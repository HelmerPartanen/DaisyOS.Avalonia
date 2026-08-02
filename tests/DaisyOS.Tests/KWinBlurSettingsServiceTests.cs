using DaisyOS.Shell.Services;
using DaisyOS.System.Processes;
using Xunit;

namespace DaisyOS.Tests;

public sealed class KWinBlurSettingsServiceTests
{
    [Fact]
    public void StrengthMapsToConfiguredBlurStrength()
    {
        Assert.Equal(10, KWinBlurSettingsService.MapStrength(50));
    }

    [Fact]
    public async Task LiveKWinSessionReceivesConfigAndReload()
    {
        var runner = new ScriptedCommandRunner(
            Success("/KWin"),
            Success(),
            Success(),
            Success(),
            Success());
        var service = new KWinBlurSettingsService(runner);

        await service.ApplyStrengthAsync(50);

        Assert.Collection(
            runner.Calls,
            call =>
            {
                Assert.Equal("qdbus6", call.FileName);
                Assert.Equal(["org.kde.KWin"], call.Arguments);
            },
            call =>
            {
                Assert.Equal("kwriteconfig6", call.FileName);
                Assert.Equal(
                    ["--file", "kwinrc", "--group", "Effect-blur", "--key", "BlurStrength", "--notify", "10"],
                    call.Arguments);
            },
            call =>
            {
                Assert.Equal("kwriteconfig6", call.FileName);
                Assert.Equal(
                    ["--file", "kwinrc", "--group", "Effect-blur", "--key", "Saturation", "--notify", "132"],
                    call.Arguments);
            },
            call =>
            {
                Assert.Equal("kwriteconfig6", call.FileName);
                Assert.Equal(
                    ["--file", "kwinrc", "--group", "Effect-blur", "--key", "NoiseStrength", "--notify", "1"],
                    call.Arguments);
            },
            call =>
            {
                Assert.Equal("dbus-send", call.FileName);
                Assert.Equal(
                    [
                        "--session",
                        "--type=method_call",
                        "--dest=org.kde.KWin",
                        "/Effects",
                        "org.kde.kwin.Effects.reconfigureEffect",
                        "string:blur"
                    ],
                    call.Arguments);
            });
    }

    [Fact]
    public async Task MissingKWinSessionDoesNotChangeConfiguration()
    {
        var runner = new ScriptedCommandRunner(new CommandResult(1, "", "not found", TimedOut: false));
        var service = new KWinBlurSettingsService(runner);

        await service.ApplyStrengthAsync(75);

        Assert.Single(runner.Calls);
        Assert.Equal("qdbus6", runner.Calls[0].FileName);
    }

    private static CommandResult Success(string output = "") =>
        new(0, output, "", TimedOut: false);
}
