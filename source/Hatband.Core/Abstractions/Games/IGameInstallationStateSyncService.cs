namespace Hatband.Core.Abstractions.Games;

public interface IGameInstallationStateSyncService
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
