using Hatband.Core.Models.Authentication;

namespace Hatband.Core.Abstractions.Authentication;

/// <summary>
/// Exposes and manages an authenticated session for a connector.
/// </summary>
public interface IConnectorSessionProvider : IConnectorAuthenticationCapability
{
    ConnectorAccount? CurrentAccount { get; }

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
