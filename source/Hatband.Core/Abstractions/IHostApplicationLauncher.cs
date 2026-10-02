using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Abstractions;

public interface IHostApplicationLauncher
{
    Task<bool> TryOpenUriAsync(Uri uri, CancellationToken cancellationToken = default);

    Task<bool> TryLaunchStoreClientAsync(GameSourceId sourceId, CancellationToken cancellationToken = default);
}
