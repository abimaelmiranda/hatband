namespace Hatband.Core.Abstractions.Games;

public interface IGameInstallationStateSyncService
{
    /// <summary>
    /// Reconciles locally installed games with the saved library without refreshing game metadata.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
