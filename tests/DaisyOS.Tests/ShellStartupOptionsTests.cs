using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class ShellStartupOptionsTests
{
    [Fact]
    public void ParsesOnboardingFlagsCaseInsensitively()
    {
        var options = ShellStartupOptions.Parse(["--SHOW-ONBOARDING"]);

        Assert.True(options.ForceOnboarding);
        Assert.False(options.SkipOnboarding);
    }

    [Fact]
    public void SkipTakesPrecedenceWhenBothFlagsArePresent()
    {
        var options = ShellStartupOptions.Parse(["--show-onboarding", "--skip-onboarding"]);

        Assert.False(options.ForceOnboarding);
        Assert.True(options.SkipOnboarding);
    }

    [Fact]
    public void IgnoresUnrelatedStartupArguments()
    {
        var options = ShellStartupOptions.Parse(["--real-services"]);

        Assert.False(options.ForceOnboarding);
        Assert.False(options.SkipOnboarding);
        Assert.False(options.ProductionMode);
    }

    [Fact]
    public void ParsesProductionModeWithoutChangingOnboardingChoice()
    {
        var options = ShellStartupOptions.Parse(["--production", "--show-onboarding"]);

        Assert.True(options.ProductionMode);
        Assert.True(options.ForceOnboarding);
        Assert.False(options.SkipOnboarding);
    }

    [Theory]
    [InlineData("--production")]
    [InlineData("--real-services")]
    public void ProductionAndRealServiceFlagsSelectRealServices(string flag)
    {
        Assert.Equal(ShellServiceMode.Real, ShellServices.SelectMode([flag]));
    }

    [Fact]
    public void CompletedOnboardingStaysClosedOnNormalLaunches()
    {
        var options = ShellStartupOptions.Parse([]);

        Assert.False(options.ShouldShowOnboarding(onboardingIncomplete: false));
    }

    [Fact]
    public void IncompleteOnboardingOpensOnNormalLaunches()
    {
        var options = ShellStartupOptions.Parse([]);

        Assert.True(options.ShouldShowOnboarding(onboardingIncomplete: true));
    }

    [Fact]
    public void ForceFlagReopensCompletedOnboarding()
    {
        var options = ShellStartupOptions.Parse(["--show-onboarding"]);

        Assert.True(options.ShouldShowOnboarding(onboardingIncomplete: false));
    }

    [Fact]
    public void SkipFlagBypassesIncompleteOnboarding()
    {
        var options = ShellStartupOptions.Parse(["--skip-onboarding"]);

        Assert.False(options.ShouldShowOnboarding(onboardingIncomplete: true));
    }
}
