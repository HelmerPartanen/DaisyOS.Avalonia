using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ShellPerformanceProfileTests
{
    [Theory]
    [InlineData(ShellPerformanceTier.Constrained, 128, 160, 1, false, true)]
    [InlineData(ShellPerformanceTier.Balanced, 192, 224, 2, false, false)]
    [InlineData(ShellPerformanceTier.Full, 256, 320, 3, true, false)]
    public void TiersBoundOptionalVisualWork(
        ShellPerformanceTier tier, int thumbnailWidth, int artworkWidth, int concurrency,
        bool spectrum, bool reducedBlur)
    {
        var profile = ShellPerformanceProfile.Create(tier);

        Assert.Equal(thumbnailWidth, profile.ThumbnailDecodeWidth);
        Assert.Equal(artworkWidth, profile.ArtworkDecodeWidth);
        Assert.Equal(concurrency, profile.BackgroundDecodeConcurrency);
        Assert.Equal(spectrum, profile.EnableLiveSpectrum);
        Assert.Equal(reducedBlur, profile.PreferReducedBlur);
    }

    [Fact]
    public void LowMemoryMachineSelectsConstrainedTier()
    {
        var profile = ShellPerformanceProfile.Create(ShellPerformanceTier.Constrained, 4, 4L * 1024 * 1024 * 1024);

        Assert.Equal(ShellPerformanceTier.Constrained, profile.Tier);
        Assert.False(profile.EnableLiveSpectrum);
    }
}
