using Avalonia;
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
    private readonly IMediaSessionService _mediaService = new LinuxMediaSessionService(new SafeCommandRunner());
    private readonly IAudioSpectrumService _audioSpectrumService = new LinuxAudioSpectrumService();
    private readonly IWallpaperColorExtractor _colorExtractor = new WallpaperColorExtractor();
    private readonly DispatcherTimer _spectrumTimer = new() { Interval = TimeSpan.FromMilliseconds(42) };
    private readonly List<Border> _spectrumBars = [];
    private CancellationTokenSource? _refreshCancellation;
    private Bitmap? _artwork;
    private bool _isPlaying;
    private bool _spectrumUpdateInFlight;

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
        _spectrumTimer.Start();
        _ = RefreshMediaAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _mediaService.MediaChanged -= OnMediaChanged;
        _spectrumTimer.Stop();
        _refreshCancellation?.Cancel();
        _artwork?.Dispose();
        _artwork = null;
        if (_mediaService is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _audioSpectrumService.Dispose();
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
            var artwork = await LoadArtworkAsync(session?.AlbumArtPath, cancellation.Token);
            Color? tint = artwork is null ? null : await ExtractArtworkTintAsync(artwork.Value.Bytes, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                artwork?.Bitmap.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ApplySession(session, artwork?.Bitmap, tint));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // An unavailable player or malformed artwork must leave the shell responsive.
        }
    }

    private void ApplySession(MediaSession? session, Bitmap? artwork, Color? tint)
    {
        _artwork?.Dispose();
        _artwork = artwork;
        _isPlaying = session?.IsPlaying == true;

        this.FindControl<TextBlock>("MediaTitle")!.Text = session?.Title ?? "Nothing playing";
        this.FindControl<TextBlock>("MediaArtist")!.Text = session is null
            ? "Start media to see controls"
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
        image.Source = artwork;
        image.IsVisible = artwork is not null;
        fallback.IsVisible = artwork is null;

        var tintLayer = this.FindControl<Border>("ArtworkTint")!;
        tintLayer.Background = tint is { } color
            ? new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B))
            : Brushes.Transparent;

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

    private static Border CreateSpectrumBar(double idleScale) => new()
    {
        Width = 2,
        Height = 24,
        CornerRadius = new CornerRadius(1),
        Background = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)),
        RenderTransformOrigin = RelativePoint.Center,
        RenderTransform = new ScaleTransform(1, idleScale)
    };

    private async void UpdateSpectrum()
    {
        if (_spectrumUpdateInFlight)
        {
            return;
        }

        _spectrumUpdateInFlight = true;
        try
        {
            var spectrum = await _audioSpectrumService.GetSpectrumAsync();
            for (var index = 0; index < _spectrumBars.Count; index++)
            {
                // The analyser returns six logarithmic ranges: bass on the left, treble on the right.
                var amplitude = index < spectrum.Count ? spectrum[index] : 0;
                var scale = 0.12 + (Math.Clamp(amplitude, 0, 1) * 0.88);
                _spectrumBars[index].RenderTransform = new ScaleTransform(1, scale);
            }
        }
        catch
        {
            foreach (var bar in _spectrumBars)
            {
                bar.RenderTransform = new ScaleTransform(1, 0.12);
            }
        }
        finally
        {
            _spectrumUpdateInFlight = false;
        }
    }

    private async Task<Color> ExtractArtworkTintAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        return await _colorExtractor.ExtractSeedAsync(stream, cancellationToken);
    }

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
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                bytes = await client.GetByteArrayAsync(uri, cancellationToken);
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
