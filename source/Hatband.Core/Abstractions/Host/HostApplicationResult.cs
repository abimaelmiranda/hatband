namespace Hatband.Core.Abstractions.Host;

/// <summary>
/// Result of a host command whose output and completion are required by its caller.
/// </summary>
public sealed record HostApplicationResult(int ExitCode, string StandardOutput, string StandardError);
