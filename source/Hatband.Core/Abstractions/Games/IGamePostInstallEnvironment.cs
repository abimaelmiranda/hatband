using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Core.Abstractions.Games;

public interface IGamePostInstallEnvironment
{
    Task<bool> Is64BitWindowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a source path template against the absolute game install directory and returns a host-native path.
    /// </summary>
    Task<string> ResolvePathAsync(
        string template,
        string installDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expands path placeholders inside a Windows registry string while preserving the Windows string format.
    /// </summary>
    Task<string> ResolveStringAsync(
        string template,
        string installDirectory,
        CancellationToken cancellationToken = default);

    Task<GamePostInstallRegistryEntry?> ReadRegistryValueAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        string valueName,
        CancellationToken cancellationToken = default);

    Task EnsureRegistryKeyAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        CancellationToken cancellationToken = default);

    Task SetRegistryValueAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        GamePostInstallRegistryEntry value,
        CancellationToken cancellationToken = default);

    Task<GamePostInstallProcessResult> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default);

    Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}
