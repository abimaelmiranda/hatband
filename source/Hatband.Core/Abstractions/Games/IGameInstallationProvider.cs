using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

public interface IGameInstallationProvider
{
    GameSourceId SourceId { get; }

    /// <summary>
    /// Returns installed games keyed by their ID in this provider's source.
    /// </summary>
    Task<IReadOnlyDictionary<string, GameInstallationInfo>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default);
}
