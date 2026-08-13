using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;

namespace DaisyOS.Shell.Services.Theming;

/// <summary>Applies wallpaper-aware desktop-label contrast; OSTheme owns all shell colours.</summary>
public sealed class DynamicThemeService
{
    private readonly IWallpaperColorExtractor _extractor;
    private CancellationTokenSource? _refreshCancellation;

    public DynamicThemeService(IWallpaperColorExtractor extractor, IDynamicSchemeGenerator generator)
    {
        _extractor = extractor;
        _ = generator; // Kept for constructor compatibility while wallpaper accents are disabled.
    }

    public async Task RefreshFromWallpaperAsync(string wallpaperUri, ThemeVariant theme, CancellationToken cancellationToken = default)
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = _refreshCancellation.Token;
        var palette = await _extractor.ExtractPaletteAsync(wallpaperUri, token).ConfigureAwait(false);
        var desktopLabelColor = WallpaperLabelContrast.ForWallpaper(palette.PrimarySeed);
        await ApplySchemeAsync(default, desktopLabelColor, palette, token).ConfigureAwait(false);
    }

    public Task ApplySchemeAsync(
        DynamicColorScheme? scheme,
        Color desktopLabelColor,
        WallpaperPalette palette = default,
        CancellationToken cancellationToken = default) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resources = Application.Current?.Resources;
            if (resources is null)
            {
                return;
            }

            Apply(resources, scheme, desktopLabelColor, palette);
        }).GetTask();

    internal static void Apply(IResourceDictionary resources, DynamicColorScheme? scheme, Color desktopLabelColor, WallpaperPalette palette)
    {
        Set(resources, "DesktopItemLabelBrush", desktopLabelColor);
    }

    private static void Set(IResourceDictionary resources, string key, Color color) =>
        resources[key] = new SolidColorBrush(color);

}
