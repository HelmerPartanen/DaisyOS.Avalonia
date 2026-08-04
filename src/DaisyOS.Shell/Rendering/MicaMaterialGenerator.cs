using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace DaisyOS.Shell.Rendering;

public struct MicaTheme
{
    public SKColor TintColor { get; set; }

    /// <summary>
    /// Theme tint strength, from 0 to 1.
    /// Controls how strongly the theme color dyes the wallpaper.
    /// </summary>
    public float TintOpacity { get; set; }

    /// <summary>
    /// Luminosity opacity, from 0 to 1.
    /// Controls how much the wallpaper's brightness is forced to match the theme color, ensuring text contrast.
    /// </summary>
    public float LuminosityOpacity { get; set; }

    /// <summary>
    /// Wallpaper saturation multiplier.
    /// </summary>
    public float Saturation { get; set; }

    public static MicaTheme DarkBase => new()
    {
        TintColor = new SKColor(32, 32, 32),
        TintOpacity = 0.70f,
        LuminosityOpacity = 0.85f,
        Saturation = 1.0f
    };

    public static MicaTheme LightBase => new()
    {
        TintColor = new SKColor(243, 243, 243),
        TintOpacity = 0.70f,
        LuminosityOpacity = 0.85f,
        Saturation = 1.0f
    };

    public static MicaTheme DarkAlt => new()
    {
        TintColor = new SKColor(20, 20, 20),
        TintOpacity = 0.75f,
        LuminosityOpacity = 0.90f,
        Saturation = 1.0f
    };

    public static MicaTheme LightAlt => new()
    {
        TintColor = new SKColor(230, 230, 230),
        TintOpacity = 0.75f,
        LuminosityOpacity = 0.90f,
        Saturation = 1.0f
    };
}

public static class MicaMaterialGenerator
{
    // The process (blur) image is sized off this reference dimension rather than a fixed
    // ratio of the render size. 240px is what a 1920x1080 render already produced under the
    // old "renderWidth / 8" scheme, so behavior at that reference resolution is unchanged.
    //
    // Using a fixed target instead of a fixed ratio is what makes the Mica blur's *effective*
    // strength consistent across screen sizes. With a fixed ratio, the blur's absolute pixel
    // radius on the final image never changes (sigma * downscale factor is constant), so the
    // same blur that fully obliterates shapes at 1080p only softens them slightly relative to
    // an 8K display, and over-blurs a small window/thumbnail. Tying the process size to a
    // fixed reference dimension makes the downscale factor -- and therefore the effective
    // blur strength relative to the screen -- scale automatically with render size.
    private const int ProcessReferenceMaxDimension = 240;

    // Floor so extreme aspect ratios or tiny render targets never collapse the process
    // image into something too small to blur meaningfully.
    private const int MinimumProcessDimension = 32;

    private static readonly SKColorSpace MaterialColorSpace = SKColorSpace.CreateSrgb();

    /// <summary>
    /// Generates a complete Mica-inspired material brush from a wallpaper asset.
    ///
    /// renderWidth and renderHeight should be physical pixel dimensions,
    /// not Avalonia logical dimensions.
    /// </summary>
    public static IBrush GenerateMicaBrush(
        string assetUri,
        MicaTheme theme,
        int renderWidth = 1920,
        int renderHeight = 1080)
    {
        ValidateArguments(assetUri, renderWidth, renderHeight);

        theme = NormalizeTheme(theme);

        using var assetStream = AssetLoader.Open(new Uri(assetUri));
        using var originalBitmap = SKBitmap.Decode(assetStream);

        if (originalBitmap is null)
        {
            throw new InvalidOperationException(
                $"Failed to decode wallpaper bitmap from '{assetUri}'.");
        }

        (int processWidth, int processHeight) = CalculateProcessDimensions(
            renderWidth,
            renderHeight);

        using var processedImage = CreateProcessedWallpaper(
            originalBitmap,
            theme,
            processWidth,
            processHeight);

        using var finalImage = RenderFinalMaterial(
            processedImage,
            theme,
            renderWidth,
            renderHeight);

        return CreateAvaloniaBrush(finalImage);
    }

    /// <summary>
    /// Converts logical Avalonia dimensions into physical pixel dimensions.
    /// </summary>
    public static IBrush GenerateMicaBrush(
        string assetUri,
        MicaTheme theme,
        double logicalWidth,
        double logicalHeight,
        double renderScaling)
    {
        if (!double.IsFinite(logicalWidth) || logicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalWidth),
                "Logical width must be greater than zero.");
        }

        if (!double.IsFinite(logicalHeight) || logicalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalHeight),
                "Logical height must be greater than zero.");
        }

        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderScaling),
                "Render scaling must be greater than zero.");
        }

        int physicalWidth = Math.Max(
            1,
            checked((int)Math.Ceiling(logicalWidth * renderScaling)));

        int physicalHeight = Math.Max(
            1,
            checked((int)Math.Ceiling(logicalHeight * renderScaling)));

        return GenerateMicaBrush(
            assetUri,
            theme,
            physicalWidth,
            physicalHeight);
    }

    private static SKImage CreateProcessedWallpaper(
        SKBitmap originalBitmap,
        MicaTheme theme,
        int processWidth,
        int processHeight)
    {
        var processInfo = new SKImageInfo(
            processWidth,
            processHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque,
            MaterialColorSpace);

        using var resizedBitmap = ResizeWallpaperToFill(
            originalBitmap,
            processWidth,
            processHeight);

        using var processSurface = SKSurface.Create(processInfo)
            ?? throw new InvalidOperationException(
                "Failed to create wallpaper processing surface.");

        SKCanvas canvas = processSurface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var saturationFilter = CreateSaturationFilter(theme.Saturation);

        // This blur occurs on the process image, which is downscaled to a fixed reference
        // dimension (see ProcessReferenceMaxDimension) rather than a fixed fraction of the
        // render size. That keeps this sigma-30 blur's effective strength -- relative to
        // whatever screen this is rendered on -- consistent: it fully obliterates
        // recognizable shapes per the Mica spec whether this is a 1080p, 4K, or 8K display.
        using var blurFilter = SKImageFilter.CreateBlur(
            sigmaX: 30f,
            sigmaY: 30f,
            tileMode: SKShaderTileMode.Clamp);

        using var processPaint = new SKPaint
        {
            IsAntialias = true,
            ColorFilter = saturationFilter,
            ImageFilter = blurFilter,
            BlendMode = SKBlendMode.SrcOver
        };

        canvas.DrawBitmap(resizedBitmap, 0f, 0f, processPaint);
        canvas.Flush();

        return processSurface.Snapshot();
    }

    private static SKImage RenderFinalMaterial(
        SKImage processedWallpaper,
        MicaTheme theme,
        int renderWidth,
        int renderHeight)
    {
        // RgbaF16 (16-bit float per channel) rather than Rgba8888 here is deliberate: this
        // surface goes through five sequential blend passes (wallpaper draw, luminosity,
        // color, fallback opacity). At 8-bit precision each pass rounds its result
        // before the next one reads it, and that compounded rounding error shows up as
        // visible contour banding in smooth, low-variance regions -- exactly what a heavily
        // blurred dark wallpaper produces. Compositing in float defers all rounding to a
        // single conversion at the very end (see ConvertToEightBit), keeping the material
        // smooth without overlaying a visible texture.
        var renderInfo = new SKImageInfo(
            renderWidth,
            renderHeight,
            SKColorType.RgbaF16,
            SKAlphaType.Opaque,
            MaterialColorSpace);

        using var surface = SKSurface.Create(renderInfo)
            ?? throw new InvalidOperationException(
                "Failed to create final Mica rendering surface.");

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(theme.TintColor);

        DrawProcessedWallpaper(
            canvas,
            processedWallpaper,
            renderWidth,
            renderHeight);

        DrawThemeTint(
            canvas,
            theme,
            renderWidth,
            renderHeight);

        canvas.Flush();

        using SKImage floatComposite = surface.Snapshot();

        // The one and only place this material gets rounded to 8-bit per channel. Doing it
        // here, once, after all wallpaper and tint compositing is complete.
        return ConvertToEightBit(floatComposite, renderWidth, renderHeight);
    }

    private static SKImage ConvertToEightBit(
        SKImage floatComposite,
        int renderWidth,
        int renderHeight)
    {
        var eightBitInfo = new SKImageInfo(
            renderWidth,
            renderHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque,
            MaterialColorSpace);

        using var surface = SKSurface.Create(eightBitInfo)
            ?? throw new InvalidOperationException(
                "Failed to create 8-bit conversion surface.");

        surface.Canvas.DrawImage(floatComposite, 0f, 0f);
        surface.Canvas.Flush();

        return surface.Snapshot();
    }

    private static void DrawProcessedWallpaper(
        SKCanvas canvas,
        SKImage processedWallpaper,
        int renderWidth,
        int renderHeight)
    {
        var destination = new SKRect(
            0f,
            0f,
            renderWidth,
            renderHeight);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };

        // The process image is always downscaled to a fixed reference dimension (see
        // ProcessReferenceMaxDimension), so the magnification factor here grows with the
        // render size -- roughly 8x at 1080p, ~32x at 8K. Bilinear looks soft/faceted at
        // the larger end of that range; a Mitchell cubic resampler stays smooth throughout.
        canvas.DrawImage(
            processedWallpaper,
            destination,
            new SKSamplingOptions(SKCubicResampler.Mitchell),
            paint);
    }

    private static void DrawThemeTint(
        SKCanvas canvas,
        MicaTheme theme,
        int renderWidth,
        int renderHeight)
    {
        // 1. Luminosity Pass: Normalizes extreme bright/dark spots in the wallpaper
        // to match the theme color's brightness, guaranteeing text readability.
        if (theme.LuminosityOpacity > 0f)
        {
            using var lumPaint = new SKPaint
            {
                Color = theme.TintColor.WithAlpha(FloatToByte(theme.LuminosityOpacity)),
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Luminosity,
                IsAntialias = false
            };
            canvas.DrawRect(0f, 0f, renderWidth, renderHeight, lumPaint);
        }

        // 2. Color Pass: Dyes the underlying blurred blobs with the theme color
        if (theme.TintOpacity > 0f)
        {
            using var colorPaint = new SKPaint
            {
                Color = theme.TintColor.WithAlpha(FloatToByte(theme.TintOpacity)),
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Color,
                IsAntialias = false
            };
            canvas.DrawRect(0f, 0f, renderWidth, renderHeight, colorPaint);
            
            // 3. Fallback Opacity Pass: Ensures a minimum baseline opacity so highly vibrant
            // wallpapers don't overwhelm the tint (essentially creating an Acrylic/Mica hybrid).
            using var alphaPaint = new SKPaint
            {
                Color = theme.TintColor.WithAlpha(FloatToByte(theme.TintOpacity * 0.6f)),
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.SrcOver,
                IsAntialias = false
            };
            canvas.DrawRect(0f, 0f, renderWidth, renderHeight, alphaPaint);
        }
    }

    private static SKBitmap ResizeWallpaperToFill(
        SKBitmap source,
        int destinationWidth,
        int destinationHeight)
    {
        var destinationInfo = new SKImageInfo(
            destinationWidth,
            destinationHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        var destinationBitmap = new SKBitmap(destinationInfo);

        using var canvas = new SKCanvas(destinationBitmap);
        canvas.Clear(SKColors.Black);

        SKRect sourceRect = CalculateSourceCropRect(
            source.Width,
            source.Height,
            destinationWidth,
            destinationHeight);

        var destinationRect = new SKRect(
            0f,
            0f,
            destinationWidth,
            destinationHeight);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.Src
        };

        canvas.DrawBitmap(
            source,
            sourceRect,
            destinationRect,
            paint);

        canvas.Flush();

        return destinationBitmap;
    }

    /// <summary>
    /// Calculates the process (pre-blur) image dimensions from the render size.
    ///
    /// The process image is scaled to a fixed reference dimension rather than a fixed
    /// fraction of the render size, so that the blur's effective strength stays consistent
    /// whether this is a small window, a 1080p display, or an 8K display. See
    /// <see cref="ProcessReferenceMaxDimension"/> for the rationale.
    /// </summary>
    private static (int Width, int Height) CalculateProcessDimensions(
        int renderWidth,
        int renderHeight)
    {
        int longestSide = Math.Max(renderWidth, renderHeight);

        // Never upscale the process image beyond the render size itself -- relevant for
        // small windows/thumbnails where renderWidth/Height is already below the reference.
        double scale = Math.Min(
            1.0,
            ProcessReferenceMaxDimension / (double)longestSide);

        int processWidth = Math.Max(
            MinimumProcessDimension,
            (int)Math.Round(renderWidth * scale));

        int processHeight = Math.Max(
            MinimumProcessDimension,
            (int)Math.Round(renderHeight * scale));

        return (processWidth, processHeight);
    }

    /// <summary>
    /// Calculates a centered UniformToFill crop.
    /// </summary>
    private static SKRect CalculateSourceCropRect(
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight)
    {
        float sourceAspect =
            sourceWidth / (float)sourceHeight;

        float destinationAspect =
            destinationWidth / (float)destinationHeight;

        if (sourceAspect > destinationAspect)
        {
            // Source is wider than the destination.
            float cropWidth =
                sourceHeight * destinationAspect;

            float left =
                (sourceWidth - cropWidth) * 0.5f;

            return new SKRect(
                left,
                0f,
                left + cropWidth,
                sourceHeight);
        }

        // Source is taller than the destination.
        float cropHeight =
            sourceWidth / destinationAspect;

        float top =
            (sourceHeight - cropHeight) * 0.5f;

        return new SKRect(
            0f,
            top,
            sourceWidth,
            top + cropHeight);
    }

    private static SKColorFilter CreateSaturationFilter(
        float saturation)
    {
        saturation = Math.Clamp(saturation, 0f, 1f);

        float inverseSaturation = 1f - saturation;

        float red =
            inverseSaturation * 0.2126f;

        float green =
            inverseSaturation * 0.7152f;

        float blue =
            inverseSaturation * 0.0722f;

        float[] matrix =
        {
            red + saturation, green,              blue,              0f, 0f,
            red,              green + saturation, blue,              0f, 0f,
            red,              green,              blue + saturation, 0f, 0f,
            0f,               0f,                 0f,                 1f, 0f
        };

        return SKColorFilter.CreateColorMatrix(matrix);
    }

    private static IBrush CreateAvaloniaBrush(SKImage finalImage)
    {
        using var encodedData = finalImage.Encode(
            SKEncodedImageFormat.Png,
            quality: 100);

        if (encodedData is null)
        {
            throw new InvalidOperationException(
                "Failed to encode the generated Mica material.");
        }

        using var stream = encodedData.AsStream();

        var avaloniaBitmap = new Bitmap(stream);

        return new ImageBrush(avaloniaBitmap)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
    }

    private static MicaTheme NormalizeTheme(MicaTheme theme)
    {
        theme.TintOpacity = Math.Clamp(
            theme.TintOpacity,
            0f,
            1f);

        theme.LuminosityOpacity = Math.Clamp(
            theme.LuminosityOpacity,
            0f,
            1f);

        theme.Saturation = Math.Clamp(
            theme.Saturation,
            0f,
            1f);

        return theme;
    }

    private static void ValidateArguments(
        string assetUri,
        int renderWidth,
        int renderHeight)
    {
        if (string.IsNullOrWhiteSpace(assetUri))
        {
            throw new ArgumentException(
                "The wallpaper asset URI cannot be empty.",
                nameof(assetUri));
        }

        if (renderWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderWidth),
                "Render width must be greater than zero.");
        }

        if (renderHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderHeight),
                "Render height must be greater than zero.");
        }
    }

    private static byte FloatToByte(float value)
    {
        return (byte)Math.Clamp(
            MathF.Round(value * 255f),
            0f,
            255f);
    }
}
