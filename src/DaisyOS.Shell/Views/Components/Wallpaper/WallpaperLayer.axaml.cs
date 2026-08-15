using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;

namespace DaisyOS.Shell.Views.Components.Wallpaper;

/// <summary>The one wallpaper visual shared by the desktop and console shell experiences.</summary>
public partial class WallpaperLayer : UserControl
{
    private const double MaximumParallaxOffset = 22;
    private readonly DispatcherTimer _parallaxTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _parallaxTranslation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private App? _app;
    private WallpaperImageService? _wallpaperImageService;
    private WallpaperRenderMetrics _wallpaperMetrics;
    private bool _parallaxEnabled;
    private double _targetOffsetX;
    private double _targetOffsetY;
    private double _offsetX;
    private double _offsetY;
    private int _refreshRequestVersion;
    private bool _isLoaded;

    public WallpaperLayer()
    {
        InitializeComponent();
        WallpaperImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(1.06, 1.06),
                _parallaxTranslation
            }
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => RequestWallpaperRefresh();
        _parallaxTimer.Tick += (_, _) => AdvanceParallax();
    }

    /// <summary>Updates the console parallax target from an in-window pointer position.</summary>
    public void SetParallaxTarget(Point position, Size viewport)
    {
        if (!_parallaxEnabled || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var normalizedX = Math.Clamp((position.X / viewport.Width - 0.5) * 2, -1, 1);
        var normalizedY = Math.Clamp((position.Y / viewport.Height - 0.5) * 2, -1, 1);
        _targetOffsetX = -normalizedX * MaximumParallaxOffset;
        _targetOffsetY = -normalizedY * MaximumParallaxOffset;
        if (!_parallaxTimer.IsEnabled)
        {
            _parallaxTimer.Start();
        }
    }

    /// <summary>Enables console motion or eases the wallpaper back to its desktop position.</summary>
    public void SetConsoleParallaxEnabled(bool enabled)
    {
        _parallaxEnabled = enabled;
        if (!enabled)
        {
            _targetOffsetX = 0;
            _targetOffsetY = 0;
            if (!_parallaxTimer.IsEnabled)
            {
                _parallaxTimer.Start();
            }
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = true;
        _app = Application.Current as App;
        if (_app is null)
        {
            return;
        }

        _app.WallpaperChanged += OnWallpaperChanged;
        _ = LoadWallpaperAsync(_app.CurrentWallpaperUri);
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = false;
        Interlocked.Increment(ref _refreshRequestVersion);
        _parallaxTimer.Stop();
        if (_app is not null)
        {
            _app.WallpaperChanged -= OnWallpaperChanged;
        }

        // The service owns its rendered bitmap. Drop the Image reference before disposal so
        // Avalonia never measures a bitmap whose native render target has been released.
        WallpaperImage.Source = null;
        _wallpaperImageService?.Dispose();
        _wallpaperImageService = null;
    }

    private void OnWallpaperChanged(object? sender, string wallpaperUri) => _ = LoadWallpaperAsync(wallpaperUri);

    private async Task LoadWallpaperAsync(string wallpaperUri)
    {
        try
        {
            var requestVersion = Interlocked.Increment(ref _refreshRequestVersion);
            WallpaperImage.Source = null;
            _wallpaperImageService?.Dispose();
            _wallpaperImageService = new WallpaperImageService(wallpaperUri);
            await RefreshWallpaperAsync(requestVersion);
        }
        catch (ArgumentException)
        {
            // Retain the embedded fallback wallpaper if the persisted value is unusable.
        }
    }

    private void RequestWallpaperRefresh()
    {
        if (_isLoaded)
        {
            _ = RefreshWallpaperAsync(Interlocked.Increment(ref _refreshRequestVersion));
        }
    }

    private async Task RefreshWallpaperAsync(int requestVersion)
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

            // A new render replaces and disposes the previous cached bitmap in the service.
            // Release the old visual reference before requesting that replacement.
            WallpaperImage.Source = null;
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
                WallpaperImage.Source = bitmap;
            }
        }
        catch
        {
            // Wallpaper refresh is cosmetic; preserve the last successfully rendered bitmap.
        }
        finally
        {
            if (refreshGateAcquired)
            {
                _refreshGate.Release();
            }
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
