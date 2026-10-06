namespace Hatband.Core.Abstractions.Host;

/// <summary>
/// A started host process with an awaitable exit result.
/// </summary>
public sealed record HostApplicationProcess(Task<int> Completion);
