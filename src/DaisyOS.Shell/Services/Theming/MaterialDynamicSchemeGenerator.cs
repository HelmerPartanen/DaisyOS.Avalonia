using Avalonia.Media;
using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Schemes;

namespace DaisyOS.Shell.Services.Theming;

/// <summary>Maps Google's HCT TonalSpot palette to Material 3 semantic roles.</summary>
public sealed class MaterialDynamicSchemeGenerator : IDynamicSchemeGenerator
{
    public DynamicColorScheme Generate(Color seed, bool isDark, bool isGrayscale = false)
    {
        var palette = new CorePalette();
        palette.Fill(ToArgb(seed), Style.TonalSpot);
        Scheme<uint> scheme = isDark
            ? new DarkSchemeMapper().Map(palette)
            : new LightSchemeMapper().Map(palette);

        var primary = FromArgb(scheme.Primary);
        var onPrimary = FromArgb(scheme.OnPrimary);
        var primaryContainer = FromArgb(scheme.PrimaryContainer);
        var onPrimaryContainer = FromArgb(scheme.OnPrimaryContainer);

        var error = FromArgb(scheme.Error);
        var onError = FromArgb(scheme.OnError);
        var errorContainer = FromArgb(scheme.ErrorContainer);
        var onErrorContainer = FromArgb(scheme.OnErrorContainer);

        if (isDark)
        {
            // Material's dark primary is deliberately high-tone, which turns quiet wallpaper
            // hues into pastel or lime fills. DaisyOS active controls use the restrained
            // container pair instead: it keeps a forest green feeling like forest green.
            primaryContainer = Desaturate(primaryContainer, 0.62);
            onPrimaryContainer = Desaturate(onPrimaryContainer, 0.62);
            primary = primaryContainer;
            onPrimary = onPrimaryContainer;
        }
        else
        {
            // Increase contrast of PrimaryContainer against light backgrounds
            primaryContainer = Blend(primaryContainer, primary, 0.15);

            // Refine Light Mode danger/error roles for rich contrast
            error = Color.FromRgb(211, 47, 47);
            onError = Color.FromRgb(255, 255, 255);
            errorContainer = Color.FromRgb(252, 232, 230);
            onErrorContainer = Color.FromRgb(140, 29, 24);
        }

        if (isGrayscale)
        {
            // For a truly black & white wallpaper, force the primary accent to be a crisp neutral color (White/Black)
            // instead of whatever faint hue was left over in the seed color.
            primary = isDark ? Color.FromRgb(255, 255, 255) : Color.FromRgb(0, 0, 0);
            onPrimary = isDark ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255);
            primaryContainer = isDark ? Color.FromRgb(50, 50, 50) : Color.FromRgb(220, 220, 220);
            onPrimaryContainer = isDark ? Color.FromRgb(255, 255, 255) : Color.FromRgb(0, 0, 0);
        }

        return new DynamicColorScheme(
            FromArgb(scheme.Background),
            FromArgb(scheme.OnBackground),
            FromArgb(scheme.Surface),
            FromArgb(scheme.SurfaceDim),
            FromArgb(scheme.SurfaceBright),
            FromArgb(scheme.SurfaceContainerLowest),
            FromArgb(scheme.SurfaceContainerLow),
            FromArgb(scheme.SurfaceContainer),
            FromArgb(scheme.SurfaceContainerHigh),
            FromArgb(scheme.SurfaceContainerHighest),
            FromArgb(scheme.OnSurface),
            FromArgb(scheme.OnSurfaceVariant),
            primary,
            onPrimary,
            primaryContainer,
            onPrimaryContainer,
            FromArgb(scheme.Secondary),
            FromArgb(scheme.OnSecondary),
            FromArgb(scheme.SecondaryContainer),
            FromArgb(scheme.OnSecondaryContainer),
            FromArgb(scheme.Tertiary),
            FromArgb(scheme.OnTertiary),
            FromArgb(scheme.TertiaryContainer),
            FromArgb(scheme.OnTertiaryContainer),
            FromArgb(scheme.Outline),
            FromArgb(scheme.OutlineVariant),
            error,
            onError,
            errorContainer,
            onErrorContainer,
            FromArgb(scheme.InverseSurface),
            FromArgb(scheme.InverseOnSurface),
            FromArgb(scheme.InversePrimary),
            Color.FromArgb(255, 0, 0, 0),
            FromArgb(scheme.Shadow));
    }

    private static uint ToArgb(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static Color FromArgb(uint color) => Color.FromArgb(
        (byte)(color >> 24),
        (byte)(color >> 16),
        (byte)(color >> 8),
        (byte)color);

    private static Color Blend(Color baseColor, Color accentColor, double accentWeight)
    {
        byte Channel(byte baseChannel, byte accentChannel) =>
            (byte)Math.Round(baseChannel + ((accentChannel - baseChannel) * accentWeight));

        return Color.FromArgb(
            baseColor.A,
            Channel(baseColor.R, accentColor.R),
            Channel(baseColor.G, accentColor.G),
            Channel(baseColor.B, accentColor.B));
    }

    private static Color Desaturate(Color color, double amount)
    {
        var neutral = (byte)Math.Round((color.R + color.G + color.B) / 3d);
        byte Channel(byte component) => (byte)Math.Round(component + ((neutral - component) * amount));
        return Color.FromArgb(color.A, Channel(color.R), Channel(color.G), Channel(color.B));
    }
}
