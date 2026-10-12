using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Persists authenticated connector sessions using encrypted storage.
/// </summary>
public interface IConnectorSessionStore
{
    Task<StoredConnectorCredentials<TCredentials>?> LoadAsync<TCredentials>(
        string sourceId,
        CancellationToken cancellationToken = default)
        where TCredentials : class;

    Task SaveAsync<TCredentials>(
        string sourceId,
        int version,
        TCredentials credentials,
        CancellationToken cancellationToken = default)
        where TCredentials : class;

    Task DeleteAsync(string sourceId, CancellationToken cancellationToken = default);
}
