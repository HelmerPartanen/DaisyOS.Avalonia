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
    public float TintOpacity { get; set; }
    public float Saturation { get; set; }
    public float NoiseAmplitude { get; set; }

    public static MicaTheme DarkBase => new()
    {
        TintColor = new SKColor(30, 30, 34),
        TintOpacity = 0.82f,
        Saturation = 0.68f,
        NoiseAmplitude = 0.003f
    };

    public static MicaTheme LightBase => new()
    {
        TintColor = new SKColor(243, 243, 246),
        TintOpacity = 0.84f,
        Saturation = 0.76f,
        NoiseAmplitude = 0.0025f
    };
}

public static class MicaMaterialGenerator
{
    public static IBrush GenerateMicaBrush(string assetUri, MicaTheme theme, int targetWidth = 640, int targetHeight = 360)
    {
        using var asset = AssetLoader.Open(new Uri(assetUri));
        using var originalBitmap = SKBitmap.Decode(asset);
        if (originalBitmap == null)
            throw new Exception("Failed to decode wallpaper bitmap.");

        var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var resized = originalBitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear));
        if (resized == null)
            throw new Exception("Failed to resize wallpaper bitmap.");

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // 1. Color filter for saturation
        float s = theme.Saturation;
        float sr = (1 - s) * 0.2126f;
        float sg = (1 - s) * 0.7152f;
        float sb = (1 - s) * 0.0722f;

        float[] colorMatrix = {
            sr + s, sg,     sb,     0, 0,
            sr,     sg + s, sb,     0, 0,
            sr,     sg,     sb + s, 0, 0,
            0,      0,      0,      1, 0
        };

        using var colorFilter = SKColorFilter.CreateColorMatrix(colorMatrix);
        
        // 2. Heavy blur filter (Sigma 20 at 640x360 is roughly equivalent to 40 at 1280x720)
        using var blurFilter = SKImageFilter.CreateBlur(15, 15);

        using var paint = new SKPaint
        {
            ImageFilter = blurFilter,
            ColorFilter = colorFilter
        };

        // Draw the processed wallpaper
        canvas.DrawBitmap(resized, 0, 0, paint);

        // 3. Draw the Theme Tint
        using var tintPaint = new SKPaint
        {
            Color = theme.TintColor.WithAlpha((byte)(theme.TintOpacity * 255)),
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.SrcOver
        };
        canvas.DrawRect(0, 0, targetWidth, targetHeight, tintPaint);

        // 4. (Optional) Noise layer could go here, omitting for performance on startup in MVI

        canvas.Flush();
        using var finalImage = surface.Snapshot();
        using var data = finalImage.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

        return new ImageBrush(avaloniaBitmap)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
    }
}
