using System.Globalization;
using DaisyOS.System.Processes;

namespace DaisyOS.Shell.Services.Compositor;

/// <summary>
/// Applies the global KWin blur-effect strength used by every KWin blur region.
/// The per-window blur protocol only supplies geometry; blur kernel strength is
/// a global KWin effect setting.
/// </summary>
public sealed class KWinBlurSettingsService
{
    public const int MinimumStrength = 1;
    public const int MaximumStrength = 15;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private readonly ICommandRunner _commandRunner;

    public KWinBlurSettingsService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Clamps <paramref name="strength"/> to KWin's supported range, persists it
    /// in kwinrc, and reloads the running blur effect immediately.
    /// </summary>
    public async Task<KWinBlurStrengthResult> SetStrengthAsync(
        int strength,
        CancellationToken cancellationToken = default)
    {
        var appliedStrength = Math.Clamp(strength, MinimumStrength, MaximumStrength);
        if (!OperatingSystem.IsLinux())
        {
            return KWinBlurStrengthResult.Unsupported(strength, appliedStrength);
        }

        var value = appliedStrength.ToString(CultureInfo.InvariantCulture);
        var writeResult = await _commandRunner.RunAsync(
            "kwriteconfig6",
            ["--file", "kwinrc", "--group", "Effect-blur", "--key", "BlurStrength", value, "--notify"],
            CommandTimeout,
            cancellationToken);

        if (!writeResult.Succeeded)
        {
            return KWinBlurStrengthResult.Failed(strength, appliedStrength, "Could not save the KWin blur setting.", writeResult);
        }

        // Reconfiguring KWin generally does not rebuild the loaded blur kernel.
        // Reload the effect itself so this call has an immediate visual result.
        var reloadResult = await _commandRunner.RunAsync(
            "dbus-send",
            ["--session", "--type=method_call", "--dest=org.kde.KWin", "/Effects", "org.kde.kwin.Effects.reconfigureEffect", "string:blur"],
            CommandTimeout,
            cancellationToken);

        return reloadResult.Succeeded
            ? KWinBlurStrengthResult.Success(strength, appliedStrength)
            : KWinBlurStrengthResult.Failed(strength, appliedStrength, "KWin saved the setting but did not reload its blur effect.", reloadResult);
    }
}

/// <summary>Outcome of a global KWin blur-strength update.</summary>
public sealed record KWinBlurStrengthResult(
    int RequestedStrength,
    int AppliedStrength,
    bool Succeeded,
    bool IsSupported,
    string? FailureMessage)
{
    internal static KWinBlurStrengthResult Success(int requestedStrength, int appliedStrength) =>
        new(requestedStrength, appliedStrength, Succeeded: true, IsSupported: true, FailureMessage: null);

    internal static KWinBlurStrengthResult Unsupported(int requestedStrength, int appliedStrength) =>
        new(requestedStrength, appliedStrength, Succeeded: false, IsSupported: false, FailureMessage: "KWin blur strength is only available on Linux.");

    internal static KWinBlurStrengthResult Failed(
        int requestedStrength,
        int appliedStrength,
        string action,
        CommandResult commandResult) =>
        new(
            requestedStrength,
            appliedStrength,
            Succeeded: false,
            IsSupported: true,
            FailureMessage: FormatFailure(action, commandResult));

    private static string FormatFailure(string action, CommandResult commandResult)
    {
        var detail = commandResult.StandardError.Trim();
        if (string.IsNullOrEmpty(detail))
        {
            detail = commandResult.TimedOut ? "The command timed out." : $"Exit code {commandResult.ExitCode}.";
        }

        return $"{action} {detail}";
    }
}
