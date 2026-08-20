using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DaisyOS.Shell.Converters;

public class BitmapAssetValueConverter : IValueConverter
{
    public static BitmapAssetValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string rawUri && !string.IsNullOrWhiteSpace(rawUri))
        {
            try
            {
                var uri = rawUri.StartsWith("avares://") 
                    ? new Uri(rawUri) 
                    : new Uri($"avares://DaisyOS.Shell/{rawUri}");

                return new Bitmap(AssetLoader.Open(uri));
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
