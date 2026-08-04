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
            FromArgb(scheme.Primary),
            FromArgb(scheme.OnPrimary),
            FromArgb(scheme.PrimaryContainer),
            FromArgb(scheme.OnPrimaryContainer),
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
}
