using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;

namespace DaisyOS.Shell.Services.Theming;

/// <summary>Applies wallpaper-derived accent roles while keeping shell surface materials stable.</summary>
public sealed class DynamicThemeService
{
    private readonly IWallpaperColorExtractor _extractor;
    private readonly IDynamicSchemeGenerator _generator;
    private CancellationTokenSource? _refreshCancellation;

    public DynamicThemeService(IWallpaperColorExtractor extractor, IDynamicSchemeGenerator generator)
    {
        _extractor = extractor;
        _generator = generator;
    }

    public async Task RefreshFromWallpaperAsync(string wallpaperUri, ThemeVariant theme, CancellationToken cancellationToken = default)
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = _refreshCancellation.Token;
        var palette = await _extractor.ExtractPaletteAsync(wallpaperUri, token).ConfigureAwait(false);
        var desktopLabelColor = WallpaperLabelContrast.ForWallpaper(palette.PrimarySeed);
        var scheme = _generator.Generate(palette.PrimarySeed, theme == ThemeVariant.Dark, palette.IsGrayscale);
        await ApplySchemeAsync(scheme, desktopLabelColor, palette, token).ConfigureAwait(false);
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

        if (scheme is null)
        {
            return;
        }

        resources["AccentColor"] = scheme.Primary;
        Set(resources, "AccentBrush", scheme.Primary);
        Set(resources, "AccentHoverBrush", Blend(scheme.Primary, scheme.OnPrimary, 0.08));
        Set(resources, "AccentPressedBrush", Blend(scheme.Primary, scheme.OnPrimary, 0.16));
        Set(resources, "OnAccentBrush", scheme.OnPrimary);
        Set(resources, "AccentSurfaceBrush", scheme.PrimaryContainer);
        Set(resources, "AccentSurfaceHoverBrush", Blend(scheme.PrimaryContainer, scheme.OnPrimaryContainer, 0.08));
        Set(resources, "AccentSurfacePressedBrush", Blend(scheme.PrimaryContainer, scheme.OnPrimaryContainer, 0.16));
        Set(resources, "OnAccentSurfaceBrush", scheme.OnPrimaryContainer);
    }

    private static void Set(IResourceDictionary resources, string key, Color color) =>
        resources[key] = new SolidColorBrush(color);

    private static Color Blend(Color from, Color to, double amount) =>
        Color.FromArgb(
            (byte)Math.Round(from.A + ((to.A - from.A) * amount)),
            (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
            (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
            (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

}
