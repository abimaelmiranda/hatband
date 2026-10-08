using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Exposes and manages an authenticated session for a connector.
/// </summary>
public interface IConnectorSessionProvider : IConnectorAuthenticationCapability
{
    ConnectorSession? CurrentSession { get; }

    ConnectorAccount? CurrentAccount { get; }

    Task<bool> RestoreAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
