using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace DaisyOS.Shell.Services.Wallpaper;

/// <summary>
/// Diagnostic counters for wallpaper rendering operations.
/// </summary>
public sealed class WallpaperDiagnostics
{
    /// <summary>Number of times a file was read from disk.</summary>
    public int FileReadCount { get; internal set; }

    /// <summary>Number of times an image was decoded from a stream.</summary>
    public int BitmapDecodeCount { get; internal set; }

    /// <summary>Number of times a crop/scale pre-render operation was performed.</summary>
    public int CropScaleCount { get; internal set; }

    /// <summary>Number of times the cached bitmap was replaced.</summary>
    public int CacheReplacementCount { get; internal set; }

    /// <summary>Number of times a render call was observed (for information only).</summary>
    public int RenderCallCount { get; internal set; }

    public void Reset()
    {
        FileReadCount = 0;
        BitmapDecodeCount = 0;
        CropScaleCount = 0;
        CacheReplacementCount = 0;
        RenderCallCount = 0;
    }

    public override string ToString()
        => $"FileReads={FileReadCount}, Decodes={BitmapDecodeCount}, CropScales={CropScaleCount}, Replacements={CacheReplacementCount}, Renders={RenderCallCount}";
}

/// <summary>
/// Immutable description of the target surface for wallpaper pre-rendering.
/// </summary>
public readonly record struct WallpaperRenderMetrics(
    int PhysicalWidth,
    int PhysicalHeight,
    double Scaling,
    Stretch Stretch)
{
    public static WallpaperRenderMetrics FromPixelSize(int pixelWidth, int pixelHeight, double scaling, Stretch stretch)
        => new(pixelWidth, pixelHeight, scaling, stretch);

    public bool Equals(WallpaperRenderMetrics other)
        => PhysicalWidth == other.PhysicalWidth
        && PhysicalHeight == other.PhysicalHeight
        && Scaling.Equals(other.Scaling)
        && Stretch == other.Stretch;

    public override int GetHashCode()
        => HashCode.Combine(PhysicalWidth, PhysicalHeight, Scaling.GetHashCode(), Stretch.GetHashCode());
}

/// <summary>
/// High-performance wallpaper image service that loads, decodes, and pre-renders
/// the wallpaper bitmap once per wallpaper path and monitor configuration.
/// 
/// Expensive work (file I/O, decode, crop/scale) happens exactly once per unique
/// combination of wallpaper path and render metrics. The resulting cached bitmap
/// is assigned directly to Image.Source and reused for all subsequent renders.
/// 
/// Normal Avalonia compositor redraws are expected and not prevented.
/// </summary>
public sealed class WallpaperImageService : IAsyncDisposable
{
    private string _wallpaperPath;
    private readonly WallpaperDiagnostics _diagnostics;

    /// <summary>
    /// Global cache of decoded source bitmaps keyed by file path.
    /// The decoded bitmap is the full-resolution image as loaded from disk.
    /// Bounded to the 3 most-recently loaded paths to prevent unbounded RAM growth;
    /// each full-resolution wallpaper Bitmap can be 30–60 MB in memory.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Bitmap?> s_sourceBitmapCache = new();

    /// <summary>Insertion order used to enforce the source-cache bound.</summary>
    private static readonly Queue<string> s_sourceCacheOrder = new();

    /// <summary>
    /// Currently cached pre-rendered bitmap assigned to the Image control.
    /// </summary>
    private Bitmap? _cachedBitmap;

    /// <summary>
    /// The render metrics used to produce the current cached bitmap.
    /// </summary>
    private WallpaperRenderMetrics _currentMetrics;

    /// <summary>
    /// Cancellation token source for in-flight regeneration operations.
    /// </summary>
    private CancellationTokenSource? _regenCts;

    /// <summary>
    /// Lock to protect access to the cached bitmap and metrics during swaps.
    /// </summary>
    private readonly object _swapLock = new();

    /// <summary>
    /// Gets the current wallpaper path.
    /// </summary>
    public string WallpaperPath => _wallpaperPath;

    /// <summary>
    /// Gets the diagnostic counters for this service instance.
    /// </summary>
    public WallpaperDiagnostics Diagnostics => _diagnostics;

    /// <summary>
    /// Gets the currently cached bitmap, or null if none has been generated yet.
    /// This property returns the stored field and does not perform any I/O or allocation.
    /// </summary>
    public Bitmap? WallpaperBitmap
    {
        get
        {
            // Track render calls for diagnostics - reading the property is not a render,
            // but we count when the bitmap is actually used by the compositor.
            return _cachedBitmap;
        }
    }

    /// <summary>
    /// Event raised when the cached wallpaper bitmap is replaced.
    /// </summary>
    public event EventHandler<Bitmap?>? WallpaperBitmapChanged;

    public WallpaperImageService(string wallpaperPath, WallpaperDiagnostics? diagnostics = null)
    {
        if (string.IsNullOrWhiteSpace(wallpaperPath))
            throw new ArgumentException("Wallpaper path cannot be null or empty.", nameof(wallpaperPath));

        _wallpaperPath = wallpaperPath;
        _diagnostics = diagnostics ?? new WallpaperDiagnostics();
        _currentMetrics = default;
    }

    /// <summary>
    /// Gets or creates the cached wallpaper bitmap for the specified render metrics.
    /// If the metrics match the current cache, the existing bitmap is returned immediately.
    /// Otherwise, a new bitmap is generated asynchronously.
    /// </summary>
    public async ValueTask<Bitmap?> GetWallpaperAsync(WallpaperRenderMetrics metrics, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fast path: return cached bitmap if metrics match
        lock (_swapLock)
        {
            if (_cachedBitmap is not null && _currentMetrics.Equals(metrics))
            {
                return _cachedBitmap;
            }
        }

        // Cancel any in-flight regeneration
        _regenCts?.Cancel();
        _regenCts?.Dispose();
        _regenCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _regenCts.Token);
        var ct = linkedCts.Token;

        try
        {
            return await GenerateWallpaperAsync(metrics, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            // If cancelled, return current cached bitmap if available
            lock (_swapLock)
            {
                return _cachedBitmap;
            }
        }
    }

    /// <summary>
    /// Synchronously gets the cached wallpaper bitmap if metrics match, otherwise returns null.
    /// Use this from UI-bound code when you need the current bitmap without awaiting.
    /// </summary>
    public Bitmap? GetCurrentWallpaper(WallpaperRenderMetrics metrics)
    {
        lock (_swapLock)
        {
            return _currentMetrics.Equals(metrics) ? _cachedBitmap : null;
        }
    }

    /// <summary>
    /// Forces regeneration of the wallpaper bitmap with new metrics.
    /// The previous bitmap is disposed after the new one is ready.
    /// </summary>
    public async ValueTask<Bitmap?> RefreshWallpaperAsync(WallpaperRenderMetrics metrics, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Cancel any in-flight regeneration
        _regenCts?.Cancel();
        _regenCts?.Dispose();
        _regenCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _regenCts.Token);
        var ct = linkedCts.Token;

        try
        {
            return await GenerateWallpaperAsync(metrics, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            lock (_swapLock)
            {
                return _cachedBitmap;
            }
        }
    }

    /// <summary>
    /// Core method that generates the wallpaper bitmap. This performs the expensive
    /// operations: file read, decode, and pre-rendering.
    /// </summary>
    private async ValueTask<Bitmap?> GenerateWallpaperAsync(WallpaperRenderMetrics metrics, CancellationToken cancellationToken)
    {
        // Step 1: Load and decode the source bitmap (once per path globally)
        var sourceBitmap = await LoadSourceBitmapAsync(_wallpaperPath, _diagnostics, cancellationToken).ConfigureAwait(false);
        if (sourceBitmap is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        // Step 2: Pre-render to target size if needed
        Bitmap? resultBitmap;

        // Check if we can use the source bitmap directly (no scaling needed)
        if (CanUseSourceDirectly(sourceBitmap, metrics))
        {
            // Use the decoded bitmap directly - no crop/scale needed
            resultBitmap = sourceBitmap;
        }
        else
        {
            // Pre-render with crop and scale
            resultBitmap = await PreRenderBitmapAsync(sourceBitmap, metrics, cancellationToken).ConfigureAwait(false);
        }

        if (resultBitmap is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        // Step 3: Swap the cached bitmap atomically
        Bitmap? oldBitmap = null;
        lock (_swapLock)
        {
            oldBitmap = _cachedBitmap;
            _cachedBitmap = resultBitmap;
            _currentMetrics = metrics;
            _diagnostics.CacheReplacementCount++;
        }

        // Dispose old bitmap after swap (but not the one we're now displaying)
        if (oldBitmap is not null && oldBitmap != resultBitmap)
        {
            oldBitmap.Dispose();
        }

        // Raise change notification on UI thread
        if (Dispatcher.UIThread.CheckAccess())
        {
            WallpaperBitmapChanged?.Invoke(this, resultBitmap);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => WallpaperBitmapChanged?.Invoke(this, resultBitmap));
        }

        return resultBitmap;
    }

    /// <summary>
    /// Determines if the source bitmap can be used directly without pre-rendering.
    /// </summary>
    private static bool CanUseSourceDirectly(Bitmap source, WallpaperRenderMetrics metrics)
    {
        // If scaling is 1.0 and the source size matches the target, use directly
        // For UniformToFill, we still need to crop if aspect ratios differ
        if (metrics.Scaling != 1.0)
            return false;

        var sourceSize = source.Size;
        return Math.Abs(sourceSize.Width - metrics.PhysicalWidth) < 1
            && Math.Abs(sourceSize.Height - metrics.PhysicalHeight) < 1;
    }

    /// <summary>
    /// Loads the source bitmap from disk, using a global cache to avoid duplicate decodes.
    /// </summary>
    private static async Task<Bitmap?> LoadSourceBitmapAsync(string path, WallpaperDiagnostics diagnostics, CancellationToken cancellationToken)
    {
        // Return early from cache without Task.Run allocation cost.
        if (s_sourceBitmapCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.FileReadCount++;

            // Double-check inside Task.Run to avoid duplicate decode races.
            if (s_sourceBitmapCache.TryGetValue(path, out var existing))
            {
                return existing;
            }

            Bitmap? loaded;
            try
            {
                if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(path);
                    using var stream = AssetLoader.Open(uri);
                    loaded = new Bitmap(stream);
                    diagnostics.BitmapDecodeCount++;
                }
                else if (File.Exists(path))
                {
                    loaded = new Bitmap(path);
                    diagnostics.BitmapDecodeCount++;
                }
                else
                {
                    loaded = null;
                }
            }
            catch
            {
                loaded = null;
            }

            s_sourceBitmapCache[path] = loaded;

            // Enforce the cache bound: keep only the 3 most-recently used paths.
            lock (s_sourceCacheOrder)
            {
                s_sourceCacheOrder.Enqueue(path);
                const int MaxCachedSources = 3;
                while (s_sourceCacheOrder.Count > MaxCachedSources)
                {
                    var evictKey = s_sourceCacheOrder.Dequeue();
                    if (s_sourceBitmapCache.TryRemove(evictKey, out var evicted))
                    {
                        evicted?.Dispose();
                    }
                }
            }

            return loaded;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pre-renders the wallpaper to the target size with UniformToFill cropping.
    /// Uses a DrawingContext approach for reliable rendering in Avalonia 12.
    /// </summary>
    private async Task<Bitmap?> PreRenderBitmapAsync(Bitmap source, WallpaperRenderMetrics metrics, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _diagnostics.CropScaleCount++;

            try
            {
                var sourceSize = source.Size;
                var targetWidth = metrics.PhysicalWidth;
                var targetHeight = metrics.PhysicalHeight;

                // Calculate the source rectangle for UniformToFill
                // This preserves aspect ratio and center-crops
                var sourceRect = CalculateUniformToFillRect(sourceSize.Width, sourceSize.Height, targetWidth, targetHeight);

                // Create a render target bitmap at the target size
                var renderTarget = new RenderTargetBitmap(new PixelSize(targetWidth, targetHeight));

                // Use a DrawingContext to draw the cropped and scaled bitmap
                using (var context = renderTarget.CreateDrawingContext())
                {
                    // Draw the image with cropping and scaling
                    // DrawImage in Avalonia 12 takes (IBitmap, Rect, Rect)
                    context.DrawImage(
                        source,
                        sourceRect,  // Source rectangle (crop area)
                        new Rect(0, 0, targetWidth, targetHeight));  // Destination rectangle
                }

                return renderTarget;
            }
            catch
            {
                return null;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates the source rectangle for UniformToFill stretching.
    /// </summary>
    private static Rect CalculateUniformToFillRect(double sourceWidth, double sourceHeight, double targetWidth, double targetHeight)
    {
        double sourceAspect = sourceWidth / sourceHeight;
        double targetAspect = targetWidth / targetHeight;

        double cropWidth;
        double cropHeight;

        if (sourceAspect > targetAspect)
        {
            // Source is wider - crop horizontally
            // Scale source to fill target height, then crop width
            cropHeight = sourceHeight;
            cropWidth = targetWidth * (sourceHeight / targetHeight);
        }
        else
        {
            // Source is taller - crop vertically
            // Scale source to fill target width, then crop height
            cropWidth = sourceWidth;
            cropHeight = targetHeight * (sourceWidth / targetWidth);
        }

        // Center the crop
        var x = (sourceWidth - cropWidth) / 2.0;
        var y = (sourceHeight - cropHeight) / 2.0;

        return new Rect(x, y, cropWidth, cropHeight);
    }

    /// <summary>
    /// Replaces the wallpaper with a new path and regenerates the bitmap.
    /// </summary>
    public async ValueTask<Bitmap?> SetWallpaperAsync(string newPath, WallpaperRenderMetrics metrics, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(_wallpaperPath, newPath, StringComparison.Ordinal))
        {
            // Same path, just refresh with new metrics
            return await RefreshWallpaperAsync(metrics, cancellationToken).ConfigureAwait(false);
        }

        // Cancel any in-flight operations
        _regenCts?.Cancel();
        _regenCts?.Dispose();
        _regenCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _regenCts.Token);

        try
        {
            // Load source for new path
            var newSource = await LoadSourceBitmapAsync(newPath, _diagnostics, linkedCts.Token).ConfigureAwait(false);
            if (newSource is null)
            {
                return null;
            }

            // Pre-render
            Bitmap? resultBitmap;
            if (CanUseSourceDirectly(newSource, metrics))
            {
                resultBitmap = newSource;
            }
            else
            {
                resultBitmap = await PreRenderBitmapAsync(newSource, metrics, linkedCts.Token).ConfigureAwait(false);
            }

            if (resultBitmap is null)
            {
                return null;
            }

            // Swap
            Bitmap? oldBitmap = null;
            lock (_swapLock)
            {
                oldBitmap = _cachedBitmap;
                _cachedBitmap = resultBitmap;
                _currentMetrics = metrics;
                _diagnostics.CacheReplacementCount++;
            }

            _wallpaperPath = newPath;

            if (oldBitmap is not null && oldBitmap != resultBitmap)
            {
                oldBitmap.Dispose();
            }

            WallpaperBitmapChanged?.Invoke(this, resultBitmap);
            return resultBitmap;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            lock (_swapLock)
            {
                return _cachedBitmap;
            }
        }
    }

    /// <summary>
    /// Disposes the cached bitmap and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _regenCts?.Cancel();

        Bitmap? bitmapToDispose = null;
        lock (_swapLock)
        {
            bitmapToDispose = _cachedBitmap;
            _cachedBitmap = null;
            _currentMetrics = default;
        }

        if (bitmapToDispose is not null)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                bitmapToDispose.Dispose();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => bitmapToDispose.Dispose());
            }
        }

        _regenCts?.Dispose();
        _regenCts = null;
    }

    /// <summary>
    /// Synchronously disposes resources.
    /// </summary>
    public void Dispose()
    {
        _regenCts?.Cancel();

        Bitmap? bitmapToDispose = null;
        lock (_swapLock)
        {
            bitmapToDispose = _cachedBitmap;
            _cachedBitmap = null;
            _currentMetrics = default;
        }

        bitmapToDispose?.Dispose();
        _regenCts?.Dispose();
        _regenCts = null;
    }
}
