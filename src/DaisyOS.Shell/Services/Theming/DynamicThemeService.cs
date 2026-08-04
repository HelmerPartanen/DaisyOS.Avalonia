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
        await ApplySchemeAsync(scheme, token).ConfigureAwait(false);
    }

    public Task ApplySchemeAsync(DynamicColorScheme scheme, CancellationToken cancellationToken = default) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resources = Application.Current?.Resources;
            if (resources is null)
            {
                return;
            }

            Apply(resources, scheme);
        }).GetTask();

    private static void Apply(IResourceDictionary resources, DynamicColorScheme scheme)
    {
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
        Set(resources, "AppPrimaryBrush", scheme.Primary);
        Set(resources, "AppOnPrimaryBrush", scheme.OnPrimary);
        Set(resources, "AppPrimaryContainerBrush", scheme.PrimaryContainer);
        Set(resources, "AppOnPrimaryContainerBrush", scheme.OnPrimaryContainer);
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
    }

    private static void Set(IResourceDictionary resources, string key, Color color) =>
        resources[key] = new SolidColorBrush(color);
}
