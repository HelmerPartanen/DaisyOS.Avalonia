using Avalonia.Media;
using DaisyOS.Shell.Helpers;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ArtworkColorExtractorTests
{
    [Fact]
    public void ExtractAverageColor_ReturnsOnlyAWorkerSafeColorValue()
    {
        var method = typeof(ArtworkColorExtractor).GetMethod(
            nameof(ArtworkColorExtractor.ExtractAverageColor),
            global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.Equal(typeof(Color?), method.ReturnType);
    }

    [Fact]
    public void ExtractAverageColor_ReturnsNullWithoutArtwork()
    {
        Assert.Null(ArtworkColorExtractor.ExtractAverageColor(null));
    }
}
