namespace DaisyOS.System.Processes;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

