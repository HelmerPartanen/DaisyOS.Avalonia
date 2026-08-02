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
    /// Higher values reduce wallpaper visibility.
    /// </summary>
    public float TintOpacity { get; set; }

    /// <summary>
    /// Wallpaper saturation multiplier.
    /// 0 produces grayscale and 1 preserves original saturation.
    /// </summary>
    public float Saturation { get; set; }

    /// <summary>
    /// Strength of the subtle Mica micro-noise.
    /// Recommended range: 0.001 to 0.004.
    /// </summary>
    public float NoiseAmplitude { get; set; }

    public static MicaTheme DarkBase => new()
    {
        TintColor = new SKColor(30, 30, 34),
        TintOpacity = 0.82f,
        Saturation = 0.68f,
        NoiseAmplitude = 0.0030f
    };

    public static MicaTheme LightBase => new()
    {
        TintColor = new SKColor(243, 243, 246),
        TintOpacity = 0.84f,
        Saturation = 0.76f,
        NoiseAmplitude = 0.0020f
    };

    public static MicaTheme DarkAlt => new()
    {
        TintColor = new SKColor(34, 32, 40),
        TintOpacity = 0.76f,
        Saturation = 0.72f,
        NoiseAmplitude = 0.0030f
    };

    public static MicaTheme LightAlt => new()
    {
        TintColor = new SKColor(237, 238, 244),
        TintOpacity = 0.79f,
        Saturation = 0.80f,
        NoiseAmplitude = 0.0020f
    };
}

public static class MicaMaterialGenerator
{
    private const int NoiseSeed = 0x4D494341;

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

        int processWidth = Math.Max(1, renderWidth / 3);
        int processHeight = Math.Max(1, renderHeight / 3);

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
            SKAlphaType.Opaque);

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

        // This blur occurs on the reduced-resolution image.
        // Sigma 15 at one-third resolution is equivalent to a much larger
        // blur on the final output.
        using var blurFilter = SKImageFilter.CreateBlur(
            sigmaX: 15f,
            sigmaY: 15f,
            tileMode: SKShaderTileMode.Clamp);

        using var processPaint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
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
        var renderInfo = new SKImageInfo(
            renderWidth,
            renderHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

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

        DrawMicaNoise(
            canvas,
            renderWidth,
            renderHeight,
            theme.NoiseAmplitude);

        canvas.Flush();

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
            FilterQuality = SKFilterQuality.High,
            BlendMode = SKBlendMode.SrcOver
        };

        canvas.DrawImage(
            processedWallpaper,
            destination,
            new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.None),
            paint);
    }

    private static void DrawThemeTint(
        SKCanvas canvas,
        MicaTheme theme,
        int renderWidth,
        int renderHeight)
    {
        byte tintAlpha = FloatToByte(theme.TintOpacity);

        using var tintPaint = new SKPaint
        {
            Color = theme.TintColor.WithAlpha(tintAlpha),
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.SrcOver,
            IsAntialias = false
        };

        canvas.DrawRect(
            0f,
            0f,
            renderWidth,
            renderHeight,
            tintPaint);
    }

    private static void DrawMicaNoise(
        SKCanvas canvas,
        int renderWidth,
        int renderHeight,
        float amplitude)
    {
        if (amplitude <= 0f)
        {
            return;
        }

        // Unequal, non-power-of-two dimensions make repetition less obvious.
        const int noiseWidth = 73;
        const int noiseHeight = 71;

        var noiseInfo = new SKImageInfo(
            noiseWidth,
            noiseHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var noiseBitmap = new SKBitmap(noiseInfo);

        var random = new Random(NoiseSeed);
        var pixels = new SKColor[noiseWidth * noiseHeight];

        const int neutralGray = 128;

        // Only a narrow range around neutral gray is generated.
        // Full-range 0-255 noise looks like visible television static.
        const int sourceDeviation = 15;

        // SoftLight with middle gray is close to neutral, so the layer may
        // use a slightly higher alpha while still remaining extremely subtle.
        byte layerAlpha = FloatToByte(
            Math.Clamp(amplitude * 16f, 0f, 0.08f));

        for (int i = 0; i < pixels.Length; i++)
        {
            // Difference of two uniform samples gives a triangular
            // distribution. Most values remain close to neutral gray,
            // with fewer bright or dark extremes.
            float triangularNoise =
                (float)random.NextDouble() -
                (float)random.NextDouble();

            int grayValue = neutralGray +
                (int)MathF.Round(
                    triangularNoise * sourceDeviation);

            byte gray = (byte)Math.Clamp(
                grayValue,
                0,
                255);

            pixels[i] = new SKColor(
                gray,
                gray,
                gray,
                layerAlpha);
        }

        noiseBitmap.Pixels = pixels;

        using var noiseShader = SKShader.CreateBitmap(
            noiseBitmap,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKMatrix.CreateIdentity());

        using var noisePaint = new SKPaint
        {
            Shader = noiseShader,
            BlendMode = SKBlendMode.SoftLight,
            IsAntialias = false,
            FilterQuality = SKFilterQuality.None
        };

        canvas.DrawRect(
            0f,
            0f,
            renderWidth,
            renderHeight,
            noisePaint);
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
            FilterQuality = SKFilterQuality.High,
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

        theme.Saturation = Math.Clamp(
            theme.Saturation,
            0f,
            1f);

        theme.NoiseAmplitude = Math.Clamp(
            theme.NoiseAmplitude,
            0f,
            0.01f);

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