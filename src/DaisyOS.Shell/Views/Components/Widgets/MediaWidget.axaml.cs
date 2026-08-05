using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DaisyOS.Core.Models;
using DaisyOS.Core.Services;
using DaisyOS.Shell.Services.Wallpaper;
using DaisyOS.Shell.ViewModels;
using DaisyOS.System.Audio;
using DaisyOS.System.Media;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Views.Components.Widgets;

/// <summary>Bottom-row media control with MPRIS updates and a real output-spectrum visualizer.</summary>
public partial class MediaWidget : UserControl
{
    private const int SpectrumColumns = 6;
    private static readonly HttpClient ArtworkHttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly IMediaSessionService _mediaService = new LinuxMediaSessionService(new SafeCommandRunner());
    private readonly IWallpaperColorExtractor _colorExtractor = new WallpaperColorExtractor();
    private readonly DispatcherTimer _spectrumTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly List<Border> _spectrumBars = [];
    private readonly double[] _spectrumSnapshot = new double[SpectrumColumns];
    private CancellationTokenSource? _refreshCancellation;
    private IAudioSpectrumService? _audioSpectrumService;
    private Bitmap? _artwork;
    private string? _artworkPath;
    private Color? _artworkTint;
    private bool _isPlaying;
    private bool _spectrumIsIdle = true;

    public MediaWidget()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _spectrumTimer.Tick += (_, _) => UpdateSpectrum();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        BuildSpectrum();
        _mediaService.MediaChanged += OnMediaChanged;
        _ = RefreshMediaAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _mediaService.MediaChanged -= OnMediaChanged;
        _spectrumTimer.Stop();
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        _artwork?.Dispose();
        _artwork = null;
        if (_mediaService is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _audioSpectrumService?.Dispose();
        _audioSpectrumService = null;
    }

    private void OnMediaChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = RefreshMediaAsync());

    private async Task RefreshMediaAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        var cancellation = _refreshCancellation = new CancellationTokenSource();

        try
        {
            var session = await _mediaService.GetCurrentSessionAsync(cancellation.Token);
            var artworkPath = session?.AlbumArtPath;
            var replaceArtwork = !string.Equals(_artworkPath, artworkPath, StringComparison.Ordinal);
            (Bitmap Bitmap, byte[] Bytes)? artwork = null;
            Color? tint = null;
            if (replaceArtwork)
            {
                artwork = await LoadArtworkAsync(artworkPath, cancellation.Token);
                tint = artwork is null ? null : await ExtractArtworkTintAsync(artwork.Value.Bytes, cancellation.Token);
            }

            if (cancellation.IsCancellationRequested)
            {
                artwork?.Bitmap.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ApplySession(session, artworkPath, artwork?.Bitmap, tint, replaceArtwork));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // An unavailable player or malformed artwork must leave the shell responsive.
        }
    }

    private void ApplySession(
        MediaSession? session,
        string? artworkPath,
        Bitmap? artwork,
        Color? tint,
        bool replaceArtwork)
    {
        if (replaceArtwork)
        {
            _artwork?.Dispose();
            _artwork = artwork;
            _artworkTint = tint;
            // Keep successful artwork cached for the track, but retry unavailable remote artwork
            // on a later media notification instead of permanently showing the fallback.
            _artworkPath = artwork is null && !string.IsNullOrWhiteSpace(artworkPath) ? null : artworkPath;
        }

        _isPlaying = session?.IsPlaying == true;
        UpdateSpectrumCaptureState();

        this.FindControl<TextBlock>("MediaTitle")!.Text = session?.Title ?? "Not playing";
        this.FindControl<TextBlock>("MediaArtist")!.Text = session is null
            ? ""
            : string.IsNullOrWhiteSpace(session.Artist) ? SourceLabel(session.SourceIdentity) : session.Artist;
        this.FindControl<TextBlock>("PlayPauseGlyph")!.Text = _isPlaying ? "pause" : "play_arrow";
        ToolTip.SetTip(this.FindControl<Button>("PlayPauseButton")!, _isPlaying ? "Pause" : "Play");
        this.FindControl<TextBlock>("SourceGlyph")!.Text = SourceGlyphFor(session?.SourceIdentity);

        var sourceIcon = ResolveSourceIcon(session?.SourceIdentity);
        var sourceIconControl = this.FindControl<Image>("SourceIcon")!;
        sourceIconControl.Source = sourceIcon;
        sourceIconControl.IsVisible = sourceIcon is not null;
        this.FindControl<TextBlock>("SourceGlyph")!.IsVisible = sourceIcon is null;

        var image = this.FindControl<Image>("Artwork")!;
        var fallback = this.FindControl<Border>("SourceFallback")!;
        image.Source = _artwork;
        image.IsVisible = _artwork is not null;
        fallback.IsVisible = _artwork is null;

        var tintLayer = this.FindControl<Border>("ArtworkTint")!;
        tintLayer.Background = _artworkTint is { } color
            ? new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B))
            : Brushes.Transparent;
        ApplyArtworkAccent();
    }

    private void UpdateSpectrumCaptureState()
    {
        if (_isPlaying && _audioSpectrumService is null)
        {
            _audioSpectrumService = new LinuxAudioSpectrumService();
        }

        if (_isPlaying)
        {
            _spectrumTimer.Start();
            return;
        }

        _spectrumTimer.Stop();
        if (_audioSpectrumService is not null)
        {
            _audioSpectrumService.Dispose();
            _audioSpectrumService = null;
        }

        ResetSpectrum();
    }

    private async void OnPreviousClicked(object? sender, RoutedEventArgs e) => await RunMediaCommandAsync(_mediaService.PreviousAsync);
    private async void OnPlayPauseClicked(object? sender, RoutedEventArgs e) => await RunMediaCommandAsync(_mediaService.PlayPauseAsync);
    private async void OnNextClicked(object? sender, RoutedEventArgs e) => await RunMediaCommandAsync(_mediaService.NextAsync);

    private async Task RunMediaCommandAsync(Func<CancellationToken, Task> command)
    {
        try
        {
            await command(CancellationToken.None);
            await RefreshMediaAsync();
        }
        catch
        {
            // MPRIS controls are best-effort; a player can exit between a click and dispatch.
        }
    }

    private void BuildSpectrum()
    {
        if (_spectrumBars.Count != 0 || this.FindControl<Canvas>("SpectrumCanvas") is not { } canvas)
        {
            return;
        }

        const double idleScale = 0.12;
        for (var index = 0; index < SpectrumColumns; index++)
        {
            var bar = CreateSpectrumBar(idleScale);
            Canvas.SetLeft(bar, 3 + index * 4);
            Canvas.SetTop(bar, 2);
            canvas.Children.Add(bar);
            _spectrumBars.Add(bar);
        }
    }

    private Border CreateSpectrumBar(double idleScale)
    {
        var bar = new Border
        {
            Width = 2,
            Height = 24,
            CornerRadius = new CornerRadius(1),
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new ScaleTransform(1, idleScale)
            {
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = ScaleTransform.ScaleYProperty,
                        Duration = TimeSpan.FromMilliseconds(75),
                        Easing = new CubicEaseOut()
                    }
                }
            }
        };

        bar.Background = this.FindResource("TextTertiaryBrush") as IBrush ?? Brushes.Gray;
        return bar;
    }

    private void UpdateSpectrum()
    {
        var spectrumService = _audioSpectrumService;
        if (!_isPlaying || spectrumService is null)
        {
            return;
        }

        try
        {
            spectrumService.CopySpectrum(_spectrumSnapshot);
            _spectrumIsIdle = false;
            for (var index = 0; index < _spectrumBars.Count; index++)
            {
                // The analyser returns six logarithmic ranges: bass on the left, treble on the right.
                var amplitude = _spectrumSnapshot[index];
                // Analysis remains linear; this display curve gives normal music enough
                // headroom to use the available height while preserving real band balance.
                var visualAmplitude = Math.Pow(Math.Clamp(amplitude * 1.35, 0, 1), 0.58);
                var scale = 0.12 + (visualAmplitude * 0.88);
                ((ScaleTransform)_spectrumBars[index].RenderTransform!).ScaleY = scale;
            }
        }
        catch
        {
            ResetSpectrum();
        }
    }

    private void ResetSpectrum()
    {
        if (_spectrumIsIdle)
        {
            return;
        }

        foreach (var bar in _spectrumBars)
        {
            ((ScaleTransform)bar.RenderTransform!).ScaleY = 0.12;
        }

        _spectrumIsIdle = true;
    }

    private async Task<Color> ExtractArtworkTintAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        return await _colorExtractor.ExtractSeedAsync(stream, cancellationToken);
    }

    private void ApplyArtworkAccent()
    {
        var primaryText = GetResourceColor("TextPrimaryBrush", Colors.White);
        var secondaryText = this.FindResource("TextSecondaryBrush") as IBrush ?? Brushes.White;
        var spectrumFallback = this.FindResource("TextTertiaryBrush") as IBrush ?? Brushes.Gray;
        var controlAccent = _artworkTint is { } tint
            ? new SolidColorBrush(Blend(tint, primaryText, 0.68))
            : secondaryText;
        var spectrumAccent = _artworkTint is { } spectrumTint
            ? new SolidColorBrush(Blend(spectrumTint, primaryText, 0.45))
            : spectrumFallback;

        this.FindControl<TextBlock>("PreviousGlyph")!.Foreground = controlAccent;
        this.FindControl<TextBlock>("PlayPauseGlyph")!.Foreground = controlAccent;
        this.FindControl<TextBlock>("NextGlyph")!.Foreground = controlAccent;
        foreach (var bar in _spectrumBars)
        {
            bar.Background = spectrumAccent;
        }
    }

    private Color GetResourceColor(string key, Color fallback) =>
        this.FindResource(key) is ISolidColorBrush brush ? brush.Color : fallback;

    private static Color Blend(Color artworkTint, Color textColor, double textWeight) =>
        Color.FromArgb(
            255,
            (byte)Math.Round(artworkTint.R + ((textColor.R - artworkTint.R) * textWeight)),
            (byte)Math.Round(artworkTint.G + ((textColor.G - artworkTint.G) * textWeight)),
            (byte)Math.Round(artworkTint.B + ((textColor.B - artworkTint.B) * textWeight)));

    private static async Task<(Bitmap Bitmap, byte[] Bytes)?> LoadArtworkAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            byte[] bytes;
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                bytes = await ArtworkHttpClient.GetByteArrayAsync(uri, cancellationToken);
            }
            else
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            }

            using var stream = new MemoryStream(bytes, writable: false);
            return (new Bitmap(stream), bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string SourceGlyphFor(string? source) => source?.ToLowerInvariant() switch
    {
        var value when value?.Contains("spotify") == true => "music_note",
        var value when value?.Contains("firefox") == true || value?.Contains("chrom") == true => "language",
        _ => "music_note"
    };

    private static string SourceLabel(string? source) => string.IsNullOrWhiteSpace(source) ? "Media player" : source;

    private static IImage? ResolveSourceIcon(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var iconName = source.Contains("spotify", StringComparison.OrdinalIgnoreCase) ? "spotify"
            : source.Contains("firefox", StringComparison.OrdinalIgnoreCase) ? "firefox"
            : source.Contains("chrom", StringComparison.OrdinalIgnoreCase) ? "google-chrome"
            : source.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        var iconPath = DesktopItemLoader.ResolveIconPath(iconName);
        return DesktopItemLoader.LoadBitmapSafe(iconPath);
    }
}
