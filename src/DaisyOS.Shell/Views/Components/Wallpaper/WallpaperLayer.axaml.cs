using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.Services.Wallpaper.VideoWallpaper;

namespace DaisyOS.Shell.Views.Components.Wallpaper;

/// <summary>The wallpaper visual shared by the desktop and console shell experiences.</summary>
public partial class WallpaperLayer : UserControl
{
    private const double MaximumParallaxOffset = 0;
    private const double WallpaperScale = 1.0;
    private const double EdgeSafetyInset = 0;
    private const int WallpaperFadeHalfDurationMilliseconds = 160;
    private readonly DispatcherTimer _parallaxTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _videoFrameTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _parallaxTranslation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private App? _app;
    private WallpaperImageService? _wallpaperImageService;
    private VideoWallpaperService? _videoWallpaperService;
    private WriteableBitmap? _videoBitmapA;
    private WriteableBitmap? _videoBitmapB;
    private bool _useBitmapA;
    private WallpaperRenderMetrics _wallpaperMetrics;
    private double _targetOffsetX;
    private double _targetOffsetY;
    private double _offsetX;
    private double _offsetY;
    private double _horizontalTravelLimit;
    private double _verticalTravelLimit;
    private int _refreshRequestVersion;
    private bool _isLoaded;
    private int _wallpaperTransitionVersion;
    private bool _wallpaperFadeInProgress;
    private WallpaperResources? _pendingVideoTransitionResources;

    public WallpaperLayer()
    {
        InitializeComponent();
        WallpaperImage.RenderTransform = _parallaxTranslation;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) =>
        {
            ApplyWallpaperGeometry();
            RequestWallpaperRefresh();
        };
        _parallaxTimer.Tick += (_, _) => AdvanceParallax();
        _videoFrameTimer.Tick += OnVideoFrameTick;
    }

    private Window? _attachedWindow;
    private bool _isConsoleMode;
    private bool _isGameRunning;

    public void SetConsoleNavigationParallax(double position)
    {
        _targetOffsetX = 0;
        _targetOffsetY = 0;
    }

    public void SetConsoleParallaxEnabled(bool enabled)
    {
        _targetOffsetX = 0;
        _targetOffsetY = 0;
        SetConsoleModeActive(enabled);
    }

    public void SetConsoleModeActive(bool active)
    {
        if (_isConsoleMode != active)
        {
            _isConsoleMode = active;
            UpdateVisibilityAndPauseState();
        }
    }

    public void SetGameRunningState(bool running)
    {
        if (_isGameRunning != running)
        {
            _isGameRunning = running;
            UpdateVisibilityAndPauseState();
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Loaded may be raised again while the shell's presentation tree is being composed.
        // Reopening the decoder in that case races the first frame and can dispose a bitmap
        // that Avalonia still has queued for rendering.
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        ApplyWallpaperGeometry();
        _app = Application.Current as App;
        if (_app is not null)
        {
            _app.WallpaperChanged += OnWallpaperChanged;
            _app.WindowTracker.OcclusionStateChanged += OnOcclusionStateChanged;
        }

        _attachedWindow = TopLevel.GetTopLevel(this) as Window;
        if (_attachedWindow is not null)
        {
            _attachedWindow.PropertyChanged += OnWindowPropertyChanged;
        }

        PropertyChanged += OnLayerPropertyChanged;
        UpdateVisibilityAndPauseState();

        if (_app is not null)
        {
            _ = LoadWallpaperAsync(_app.CurrentWallpaperUri);
        }
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = false;
        Interlocked.Increment(ref _refreshRequestVersion);
        _parallaxTimer.Stop();
        _videoFrameTimer.Stop();
        if (_app is not null)
        {
            _app.WallpaperChanged -= OnWallpaperChanged;
            _app.WindowTracker.OcclusionStateChanged -= OnOcclusionStateChanged;
        }

        if (_attachedWindow is not null)
        {
            _attachedWindow.PropertyChanged -= OnWindowPropertyChanged;
            _attachedWindow = null;
        }

        PropertyChanged -= OnLayerPropertyChanged;

        WallpaperImage.Source = null;
        WallpaperImage.Opacity = 1;
        DetachWallpaperResources().Dispose();
        _pendingVideoTransitionResources?.Dispose();
        _pendingVideoTransitionResources = null;
    }

    private void OnOcclusionStateChanged(object? sender, EventArgs e) => UpdateVisibilityAndPauseState();

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty || e.Property == Visual.IsVisibleProperty)
        {
            UpdateVisibilityAndPauseState();
        }
    }

    private void OnLayerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            UpdateVisibilityAndPauseState();
        }
    }

    private void UpdateVisibilityAndPauseState()
    {
        if (!_isLoaded || _videoWallpaperService is null || !_videoWallpaperService.IsLoaded)
        {
            return;
        }

        bool isVisible = IsEffectivelyVisible;
        bool isWindowMinimized = _attachedWindow?.WindowState == WindowState.Minimized;
        bool isWindowMaximizedOrFullScreen = _app?.WindowTracker.IsAnyWindowMaximizedOrFullScreen ?? false;

        bool shouldRender = isVisible
                            && !isWindowMinimized
                            && !isWindowMaximizedOrFullScreen
                            && !_isConsoleMode
                            && !_isGameRunning;

        if (shouldRender)
        {
            _videoWallpaperService.Play();
            if (!_videoFrameTimer.IsEnabled)
            {
                _videoFrameTimer.Start();
            }
        }
        else
        {
            _videoWallpaperService.Pause();
            if (_videoFrameTimer.IsEnabled)
            {
                _videoFrameTimer.Stop();
            }
        }
    }

    private void OnWallpaperChanged(object? sender, string wallpaperUri) => _ = LoadWallpaperAsync(wallpaperUri);

    private async Task LoadWallpaperAsync(string wallpaperUri)
    {
        try
        {
            var requestVersion = Interlocked.Increment(ref _refreshRequestVersion);
            var previousResources = DetachWallpaperResources();
            if (_pendingVideoTransitionResources is { } pendingResources)
            {
                // A video can be replaced before it produces its first frame. The visible
                // wallpaper is still the older one, so retain that resource for the fade and
                // release the never-presented video decoder immediately.
                previousResources.Dispose();
                previousResources = pendingResources;
                _pendingVideoTransitionResources = null;
            }

            string resolvedPath = ResolveWallpaperPath(wallpaperUri);

            // Never hand a still image to the video decoder. Besides wasting a decoder attempt,
            // that made ordinary JPEG wallpapers emit VAAPI initialisation errors.
            if (!string.IsNullOrEmpty(resolvedPath) && WallpaperFileTypes.IsVideo(resolvedPath))
            {
                _videoWallpaperService = new VideoWallpaperService();
                if (_videoWallpaperService.Load(resolvedPath))
                {
                    double targetFps = 30.0;
                    if (_videoWallpaperService.GetStats(out var stats) && stats.SrcFps > 0)
                    {
                        targetFps = stats.SrcFps;
                    }

                    _videoFrameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
                    _pendingVideoTransitionResources = previousResources;
                    UpdateVisibilityAndPauseState();
                    return;
                }

                _videoWallpaperService.Dispose();
                _videoWallpaperService = null;
            }

            _wallpaperImageService = new WallpaperImageService(wallpaperUri);
            await RefreshWallpaperAsync(requestVersion, previousResources);
        }
        catch (ArgumentException)
        {
            // Fallback
        }
    }

    private static string ResolveWallpaperPath(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return "src/DaisyOS.Shell/Assets/Wallpapers/Cat.mp4";

        if (File.Exists(uri))
            return uri;

        string relativePath = uri.Replace("avares://DaisyOS.Shell/", "src/DaisyOS.Shell/");
        if (File.Exists(relativePath))
            return relativePath;

        string defaultTarget = "src/DaisyOS.Shell/Assets/Wallpapers/Cat.mp4";
        if (File.Exists(defaultTarget))
            return defaultTarget;

        return uri;
    }

    private void OnVideoFrameTick(object? sender, EventArgs e)
    {
        if (_videoWallpaperService is null || !_videoWallpaperService.IsLoaded)
            return;

        if (_videoWallpaperService.TryGetFrame(out var frame))
        {
            try
            {
                int width = frame.Width;
                int height = frame.Height;

                if (width > 0 && height > 0)
                {
                    ulong ptrVal = (ulong)frame.Stride0 | ((ulong)frame.Stride1 << 32);
                    if (ptrVal != 0)
                    {
                        if (_videoBitmapA is null || _videoBitmapA.PixelSize.Width != width || _videoBitmapA.PixelSize.Height != height)
                        {
                            _videoBitmapA?.Dispose();
                            _videoBitmapB?.Dispose();
                            _videoBitmapA = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                            _videoBitmapB = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                        }

                        _useBitmapA = !_useBitmapA;
                        var targetBitmap = _useBitmapA ? _videoBitmapA : _videoBitmapB;

                        if (targetBitmap is not null)
                        {
                            using (var lockedBuf = targetBitmap.Lock())
                            {
                                unsafe
                                {
                                    long bytesToCopy = (long)width * height * 4;
                                    Buffer.MemoryCopy((void*)ptrVal, (void*)lockedBuf.Address, bytesToCopy, bytesToCopy);
                                    NativeMemory.Free((void*)ptrVal);
                                }
                            }

                            PresentWallpaper(targetBitmap, _pendingVideoTransitionResources);
                            _pendingVideoTransitionResources = null;
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < 4; ++i)
                {
                    int fd = frame.Fd[i];
                    if (fd >= 0)
                    {
                        try { LibC.close(fd); } catch { }
                    }
                }
                if (frame.AcquireFence >= 0)
                {
                    try { LibC.close(frame.AcquireFence); } catch { }
                }
            }
        }
    }

    private static class LibC
    {
        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);
    }

    private void RequestWallpaperRefresh()
    {
        if (_isLoaded && _wallpaperImageService is not null)
        {
            _ = RefreshWallpaperAsync(Interlocked.Increment(ref _refreshRequestVersion));
        }
    }

    private void ApplyWallpaperGeometry()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        double targetW = Bounds.Width * WallpaperScale;
        double targetH = Bounds.Height * WallpaperScale;

        if (Math.Abs(WallpaperImage.Width - targetW) < 0.5 && Math.Abs(WallpaperImage.Height - targetH) < 0.5)
        {
            return;
        }

        WallpaperImage.Width = targetW;
        WallpaperImage.Height = targetH;
        _horizontalTravelLimit = Math.Max(0, Math.Min(
            MaximumParallaxOffset,
            (WallpaperImage.Width - Bounds.Width) / 2 - EdgeSafetyInset));
        _verticalTravelLimit = Math.Max(0, Math.Min(
            MaximumParallaxOffset,
            (WallpaperImage.Height - Bounds.Height) / 2 - EdgeSafetyInset));
        _targetOffsetX = Math.Clamp(_targetOffsetX, -_horizontalTravelLimit, _horizontalTravelLimit);
        _targetOffsetY = Math.Clamp(_targetOffsetY, -_verticalTravelLimit, _verticalTravelLimit);
    }

    private async Task RefreshWallpaperAsync(int requestVersion, WallpaperResources? previousResources = null)
    {
        if (_wallpaperImageService is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var refreshGateAcquired = false;
        try
        {
            await _refreshGate.WaitAsync();
            refreshGateAcquired = true;
            if (!_isLoaded || requestVersion != Volatile.Read(ref _refreshRequestVersion))
            {
                return;
            }

            var wallpaperService = _wallpaperImageService;
            if (wallpaperService is null)
            {
                return;
            }

            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            _wallpaperMetrics = WallpaperRenderMetrics.FromPixelSize(
                (int)Math.Round(Bounds.Width * scaling),
                (int)Math.Round(Bounds.Height * scaling),
                scaling,
                Stretch.UniformToFill);
            var bitmap = await wallpaperService.GetWallpaperAsync(_wallpaperMetrics);
            if (_isLoaded
                && requestVersion == Volatile.Read(ref _refreshRequestVersion)
                && ReferenceEquals(wallpaperService, _wallpaperImageService)
                && bitmap is not null)
            {
                PresentWallpaper(bitmap, previousResources);
            }
        }
        catch
        {
            // Cosmetic fallback.
        }
        finally
        {
            if (refreshGateAcquired)
            {
                _refreshGate.Release();
            }
        }
    }

    private WallpaperResources DetachWallpaperResources()
    {
        _videoFrameTimer.Stop();
        var resources = new WallpaperResources(
            _wallpaperImageService,
            _videoWallpaperService,
            _videoBitmapA,
            _videoBitmapB);
        _wallpaperImageService = null;
        _videoWallpaperService = null;
        _videoBitmapA = null;
        _videoBitmapB = null;
        _useBitmapA = false;
        return resources;
    }

    private void PresentWallpaper(IImage source, WallpaperResources? previousResources)
    {
        // Live wallpapers update the incoming source every frame. Once the first frame has
        // started a fade, let subsequent frames refresh it without restarting that motion.
        if (previousResources is null && _wallpaperFadeInProgress)
        {
            WallpaperImage.Source = source;
            return;
        }

        bool shouldAnimate = previousResources?.HasResources == true
                             && WallpaperImage.Source is not null
                             && !(_app?.ReduceMotionEnabled ?? false);

        if (!shouldAnimate)
        {
            WallpaperImage.Source = source;
            WallpaperImage.Opacity = 1;
            if (previousResources is not null)
            {
                _ = RetireResourcesAfterSourceReplacementAsync(previousResources);
            }
            return;
        }

        _ = FadeToWallpaperAsync(source, previousResources!);
    }

    private async Task FadeToWallpaperAsync(IImage source, WallpaperResources previousResources)
    {
        int transitionVersion = Interlocked.Increment(ref _wallpaperTransitionVersion);
        _wallpaperFadeInProgress = true;
        WallpaperImage.Opacity = 0;
        await Task.Delay(WallpaperFadeHalfDurationMilliseconds);

        if (!_isLoaded || transitionVersion != Volatile.Read(ref _wallpaperTransitionVersion))
        {
            // A newer selection owns the source replacement now. Leave the old resources
            // alive long enough for that replacement to reach the compositor.
            await Task.Delay(WallpaperFadeHalfDurationMilliseconds * 2);
            previousResources.Dispose();
            return;
        }

        WallpaperImage.Source = source;
        WallpaperImage.Opacity = 1;
        _wallpaperFadeInProgress = false;

        await RetireResourcesAfterSourceReplacementAsync(previousResources);
    }

    private static async Task RetireResourcesAfterSourceReplacementAsync(WallpaperResources resources)
    {
        // Keep retired data through the rest of the fade. This protects compositors that
        // retain a source for more than one frame while preparing the replacement.
        await Task.Delay(WallpaperFadeHalfDurationMilliseconds);
        resources.Dispose();
    }

    private sealed class WallpaperResources(
        WallpaperImageService? imageService,
        VideoWallpaperService? videoService,
        WriteableBitmap? bitmapA,
        WriteableBitmap? bitmapB) : IDisposable
    {
        public bool HasResources => imageService is not null || videoService is not null || bitmapA is not null || bitmapB is not null;

        public void Dispose()
        {
            imageService?.Dispose();
            videoService?.Dispose();
            bitmapA?.Dispose();
            bitmapB?.Dispose();
        }
    }

    private void AdvanceParallax()
    {
        _offsetX += (_targetOffsetX - _offsetX) * 0.16;
        _offsetY += (_targetOffsetY - _offsetY) * 0.16;
        _parallaxTranslation.X = _offsetX;
        _parallaxTranslation.Y = _offsetY;

        if (Math.Abs(_targetOffsetX - _offsetX) < 0.05
            && Math.Abs(_targetOffsetY - _offsetY) < 0.05)
        {
            _offsetX = _targetOffsetX;
            _offsetY = _targetOffsetY;
            _parallaxTranslation.X = _offsetX;
            _parallaxTranslation.Y = _offsetY;
            _parallaxTimer.Stop();
        }
    }
}
