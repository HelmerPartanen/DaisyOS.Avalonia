using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;

namespace DaisyOS.Shell.Views.Components.Wallpaper;

/// <summary>The one wallpaper visual shared by the desktop and console shell experiences.</summary>
public partial class WallpaperLayer : UserControl
{
    // Travel is capped by each dimension's actual layout overscan, preventing exposed edges
    // on portrait and narrow displays without making the image zoom further.
    private const double MaximumParallaxOffset = 0;
    private const double WallpaperScale = 1.0;
    private const double EdgeSafetyInset = 0;
    private readonly DispatcherTimer _parallaxTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _parallaxTranslation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private App? _app;
    private WallpaperImageService? _wallpaperImageService;
    private WallpaperRenderMetrics _wallpaperMetrics;
    private double _targetOffsetX;
    private double _targetOffsetY;
    private double _offsetX;
    private double _offsetY;
    private double _horizontalTravelLimit;
    private double _verticalTravelLimit;
    private int _refreshRequestVersion;
    private bool _isLoaded;

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
    }

    /// <summary>Updates console parallax from the selected position in the controller carousel.</summary>
    public void SetConsoleNavigationParallax(double position)
    {
        _targetOffsetX = 0;
        _targetOffsetY = 0;
    }

    /// <summary>Enables console motion or eases the wallpaper back to its desktop position.</summary>
    public void SetConsoleParallaxEnabled(bool enabled)
    {
        _targetOffsetX = 0;
        _targetOffsetY = 0;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyWallpaperGeometry();
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
