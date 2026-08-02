namespace DaisyOS.Core.Models;

public sealed record AppLaunchResult(bool Succeeded, string Message, int? ProcessId = null);
