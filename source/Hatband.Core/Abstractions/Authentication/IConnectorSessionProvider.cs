using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Exposes and manages an authenticated session for a connector.
/// </summary>
public interface IConnectorSessionProvider : IConnectorAuthenticationCapability
{
    ConnectorAccount? CurrentAccount { get; }

    bool IsConnected { get; }

    bool IsPersisted { get; }

    Task<bool> RestoreAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
