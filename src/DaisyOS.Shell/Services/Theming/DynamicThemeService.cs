using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DaisyOS.Shell.Services.Wallpaper;

namespace DaisyOS.Shell.Services.Theming;

/// <summary>Creates and applies Material semantic colors without rebuilding shell views.</summary>
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
        var seed = await _extractor.ExtractSeedAsync(wallpaperUri, token).ConfigureAwait(false);
        var scheme = _generator.Generate(seed, theme == ThemeVariant.Dark);
        var desktopLabelColor = WallpaperLabelContrast.ForWallpaper(seed);
        await ApplySchemeAsync(scheme, desktopLabelColor, seed, token).ConfigureAwait(false);
    }

    public Task ApplySchemeAsync(
        DynamicColorScheme scheme,
        Color desktopLabelColor,
        Color seed = default,
        CancellationToken cancellationToken = default) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resources = Application.Current?.Resources;
            if (resources is null)
            {
                return;
            }

            Apply(resources, scheme, desktopLabelColor, seed);
        }).GetTask();

    internal static void Apply(IResourceDictionary resources, DynamicColorScheme scheme, Color desktopLabelColor, Color seed)
    {
        var isDarkSurface = RelativeLuminance(scheme.Surface) < 0.5;

        Set(resources, "DesktopItemLabelBrush", desktopLabelColor);
        Set(resources, "AppBackgroundBrush", scheme.Background);
        Set(resources, "AppOnBackgroundBrush", scheme.OnBackground);
        Set(resources, "AppSurfaceBrush", scheme.Surface);
        Set(resources, "AppSurfaceDimBrush", scheme.SurfaceDim);
        Set(resources, "AppSurfaceBrightBrush", scheme.SurfaceBright);
        Set(resources, "AppSurfaceContainerLowestBrush", scheme.SurfaceContainerLowest);
        Set(resources, "AppSurfaceContainerLowBrush", scheme.SurfaceContainerLow);
        Set(resources, "AppSurfaceContainerBrush", scheme.SurfaceContainer);
        Set(resources, "AppSurfaceContainerHighBrush", scheme.SurfaceContainerHigh);
        Set(resources, "AppSurfaceContainerHighestBrush", scheme.SurfaceContainerHighest);
        Set(resources, "AppOnSurfaceBrush", scheme.OnSurface);
        Set(resources, "AppOnSurfaceVariantBrush", scheme.OnSurfaceVariant);
        SetColor(resources, "AppPrimaryColor", scheme.Primary);
        Set(resources, "AppPrimaryBrush", scheme.Primary);
        Set(resources, "AppOnPrimaryBrush", scheme.OnPrimary);
        Set(resources, "AppPrimaryContainerBrush", scheme.PrimaryContainer);
        Set(resources, "AppPrimaryContainerHoverBrush", Blend(scheme.PrimaryContainer, scheme.Primary, 0.08));
        Set(resources, "AppPrimaryContainerPressedBrush", Blend(scheme.PrimaryContainer, scheme.Primary, 0.16));
        Set(resources, "AppOnPrimaryContainerBrush", scheme.OnPrimaryContainer);
        Set(resources, "QuickSettingActiveDividerBrush", Blend(scheme.PrimaryContainer, scheme.OnPrimaryContainer, 0.14));
        Set(resources, "AppSecondaryBrush", scheme.Secondary);
        Set(resources, "AppOnSecondaryBrush", scheme.OnSecondary);
        Set(resources, "AppSecondaryContainerBrush", scheme.SecondaryContainer);
        Set(resources, "AppOnSecondaryContainerBrush", scheme.OnSecondaryContainer);
        Set(resources, "AppTertiaryBrush", scheme.Tertiary);
        Set(resources, "AppOnTertiaryBrush", scheme.OnTertiary);
        Set(resources, "AppTertiaryContainerBrush", scheme.TertiaryContainer);
        Set(resources, "AppOnTertiaryContainerBrush", scheme.OnTertiaryContainer);
        Set(resources, "AppOutlineBrush", scheme.Outline);
        Set(resources, "AppOutlineVariantBrush", scheme.OutlineVariant);
        Set(resources, "AppErrorBrush", scheme.Error);
        Set(resources, "AppOnErrorBrush", scheme.OnError);
        Set(resources, "AppErrorContainerBrush", scheme.ErrorContainer);
        Set(resources, "AppOnErrorContainerBrush", scheme.OnErrorContainer);
        Set(resources, "AppInverseSurfaceBrush", scheme.InverseSurface);
        Set(resources, "AppInverseOnSurfaceBrush", scheme.InverseOnSurface);
        Set(resources, "AppInversePrimaryBrush", scheme.InversePrimary);
        Set(resources, "AppScrimBrush", scheme.Scrim);
        Set(resources, "AppShadowBrush", scheme.Shadow);

        // Shell chrome stays opaque until an actual acrylic material exists. It still
        // inherits the wallpaper palette, so launcher and system-bar surfaces feel
        // cohesive without showing a distracting unblurred wallpaper beneath them.
        if (isDarkSurface)
        {
            Set(resources, "LauncherMaterialBrush", scheme.SurfaceContainerHigh);
            Set(resources, "SystemBarMaterialBrush", scheme.SurfaceContainerHigh);
            Set(resources, "TaskbarMaterialBrush", scheme.SurfaceContainerHigh);
            Set(resources, "TaskbarBackgroundBrush", scheme.SurfaceContainerHigh);
            Set(resources, "LauncherFooterBrush", scheme.SurfaceContainerHighest);
            Set(resources, "ShellSurfaceBorderBrush", WithAlpha(scheme.OutlineVariant, 105));
        }
        else
        {
            // Base Light surface #F6F7F9 with a soft 5% wallpaper tint: clean, luminous, elegant
            var tintSource = seed.A > 0 ? seed : scheme.Primary;
            var baseLight = Color.FromRgb(246, 247, 249);
            var lightMaterial = Blend(baseLight, tintSource, 0.05);
            var lightLauncher = Blend(baseLight, tintSource, 0.04);
            var lightFooter = Blend(Color.FromRgb(240, 241, 243), tintSource, 0.06);

            Set(resources, "LauncherMaterialBrush", lightLauncher);
            Set(resources, "SystemBarMaterialBrush", lightMaterial);
            Set(resources, "TaskbarMaterialBrush", lightMaterial);
            Set(resources, "TaskbarBackgroundBrush", lightMaterial);
            Set(resources, "LauncherFooterBrush", lightFooter);
            Set(resources, "ShellSurfaceBorderBrush", WithAlpha(scheme.OutlineVariant, 82));
        }
    }

    private static void Set(IResourceDictionary resources, string key, Color color) =>
        resources[key] = new SolidColorBrush(color);

    private static void SetColor(IResourceDictionary resources, string key, Color color) =>
        resources[key] = color;

    private static Color Blend(Color background, Color foreground, double amount) =>
        Color.FromArgb(
            255,
            (byte)Math.Round(background.R + ((foreground.R - background.R) * amount)),
            (byte)Math.Round(background.G + ((foreground.G - background.G) * amount)),
            (byte)Math.Round(background.B + ((foreground.B - background.B) * amount)));

    private static Color WithAlpha(Color color, int alpha) =>
        Color.FromArgb((byte)alpha, color.R, color.G, color.B);

    private static double RelativeLuminance(Color color) =>
        (0.2126 * color.R + (0.7152 * color.G) + (0.0722 * color.B)) / 255;
}
