using Avalonia.Media;
using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Schemes;

namespace DaisyOS.Shell.Services.Theming;

/// <summary>Maps Google's HCT TonalSpot palette to Material 3 semantic roles.</summary>
public sealed class MaterialDynamicSchemeGenerator : IDynamicSchemeGenerator
{
    public DynamicColorScheme Generate(Color seed, bool isDark)
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

        if (isDark)
        {
            // Tonal Spot intentionally amplifies seed chroma. Desaturate its dark-theme
            // accents so a small bright detail cannot turn a subdued scene into neon chrome.
            primary = MuteAccent(primary);
            onPrimary = MuteAccent(onPrimary);
            primaryContainer = MuteAccent(primaryContainer);
            onPrimaryContainer = MuteAccent(onPrimaryContainer);
        }
        else
        {
            // Soft, refined light accent container with high contrast dark text/icons
            primaryContainer = Blend(Color.FromRgb(234, 237, 241), primary, 0.20);
            onPrimaryContainer = Color.FromRgb(31, 35, 40);
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
            FromArgb(scheme.Error),
            FromArgb(scheme.OnError),
            FromArgb(scheme.ErrorContainer),
            FromArgb(scheme.OnErrorContainer),
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

    private static Color MuteAccent(Color color)
    {
        var luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        const double retainedChroma = 0.35;

        byte Blend(byte channel) => (byte)Math.Round(luminance + ((channel - luminance) * retainedChroma));

        return Color.FromArgb(color.A, Blend(color.R), Blend(color.G), Blend(color.B));
    }

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
}
