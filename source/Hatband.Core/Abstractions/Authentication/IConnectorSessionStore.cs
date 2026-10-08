using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Persists authenticated connector sessions using encrypted storage.
/// </summary>
public interface IConnectorSessionStore
{
    Task<ConnectorSession?> LoadAsync(GameSourceId sourceId, CancellationToken cancellationToken = default);

    Task SaveAsync(ConnectorSession session, CancellationToken cancellationToken = default);

    Task DeleteAsync(GameSourceId sourceId, CancellationToken cancellationToken = default);
}
