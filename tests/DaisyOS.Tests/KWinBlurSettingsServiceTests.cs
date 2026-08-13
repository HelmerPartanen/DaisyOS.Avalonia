using DaisyOS.Shell.Services.Compositor;
using DaisyOS.System.Processes;
using Xunit;

namespace DaisyOS.Tests;

public sealed class KWinBlurSettingsServiceTests
{
    [Fact]
    public async Task SetStrengthAsync_ClampsThenWritesAndReloadsTheBlurEffect()
    {
        var runner = new ScriptedCommandRunner(Succeeded(), Succeeded());
        var service = new KWinBlurSettingsService(runner);

        var result = await service.SetStrengthAsync(99);

        Assert.True(result.Succeeded);
        Assert.Equal(99, result.RequestedStrength);
        Assert.Equal(KWinBlurSettingsService.MaximumStrength, result.AppliedStrength);
        Assert.Collection(
            runner.Calls,
            write =>
            {
                Assert.Equal("kwriteconfig6", write.FileName);
                Assert.Equal(
                    ["--file", "kwinrc", "--group", "Effect-blur", "--key", "BlurStrength", "15", "--notify"],
                    write.Arguments);
            },
            reload =>
            {
                Assert.Equal("dbus-send", reload.FileName);
                Assert.Equal(
                    ["--session", "--type=method_call", "--dest=org.kde.KWin", "/Effects", "org.kde.kwin.Effects.reconfigureEffect", "string:blur"],
                    reload.Arguments);
            });
    }

    [Fact]
    public async Task SetStrengthAsync_DoesNotReloadWhenPersistingFails()
    {
        var runner = new ScriptedCommandRunner(new CommandResult(1, string.Empty, "kwinrc is unavailable", TimedOut: false));
        var service = new KWinBlurSettingsService(runner);

        var result = await service.SetStrengthAsync(-3);

        Assert.False(result.Succeeded);
        Assert.Equal(KWinBlurSettingsService.MinimumStrength, result.AppliedStrength);
        Assert.Single(runner.Calls);
        Assert.Contains("Could not save", result.FailureMessage);
    }

    [Fact]
    public async Task SetStrengthAsync_ReportsReloadFailureAfterTheSettingWasSaved()
    {
        var runner = new ScriptedCommandRunner(Succeeded(), new CommandResult(1, string.Empty, "KWin is not running", TimedOut: false));
        var service = new KWinBlurSettingsService(runner);

        var result = await service.SetStrengthAsync(7);

        Assert.False(result.Succeeded);
        Assert.Equal(7, result.AppliedStrength);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Contains("did not reload", result.FailureMessage);
    }

    private static CommandResult Succeeded() => new(0, string.Empty, string.Empty, TimedOut: false);
}
