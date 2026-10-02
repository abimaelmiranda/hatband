using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Abstractions;

public interface IHostApplicationLauncher
{
    Task<bool> TryOpenUriAsync(Uri uri, CancellationToken cancellationToken = default);

    Task<bool> TryLaunchStoreClientAsync(GameSourceId sourceId, CancellationToken cancellationToken = default);

    Task<bool> TryLaunchSteamGameAsync(uint appId, CancellationToken cancellationToken = default);

    bool IsSteamClientRunning();

    Task<bool> TryCloseSteamClientGracefullyAsync(CancellationToken cancellationToken = default);

    Task<bool> TryInstallSteamGameAsync(uint appId, int volumeIndex, CancellationToken cancellationToken = default);

    Task<bool> TryUninstallSteamGameAsync(uint appId, CancellationToken cancellationToken = default);
}
