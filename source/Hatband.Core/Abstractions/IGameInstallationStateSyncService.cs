namespace Hatband.Core.Abstractions;

public interface IGameInstallationStateSyncService
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
