using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Models.Authentication;
using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam.Abstractions;

/// <summary>
/// Provides an authenticated Steam player's profile and library.
/// </summary>
public interface ISteamPlayerService
{
    ConnectorAccount? CurrentAccount { get; }

    void Disconnect();

    /// <summary>
    /// Starts a QR-code login flow.
    /// </summary>
    Task<IQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the games owned by the currently authenticated Steam account.
    /// </summary>
    Task<IReadOnlyList<SteamLibraryGame>> GetOwnedGamesAsync(CancellationToken cancellationToken = default);
}
