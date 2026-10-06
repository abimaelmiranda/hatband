namespace Hatband.Core.Abstractions.Host;

public interface IHostApplicationLauncher
{
    Task<bool> TryOpenUriAsync(Uri uri, CancellationToken cancellationToken = default);

    Task<bool> TryLaunchApplicationAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<bool> TryLaunchApplicationAsync(
        string executable,
        string? arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default);

    Task<HostApplicationProcess?> StartApplicationAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        CancellationToken cancellationToken = default);

    bool IsProcessRunning(string processName);

    Task<bool> TryCloseProcessGracefullyAsync(string processName, CancellationToken cancellationToken = default);
}
