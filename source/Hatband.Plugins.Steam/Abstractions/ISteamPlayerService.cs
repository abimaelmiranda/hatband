using Hatband.Core.Models.Authentication;
using Hatband.Plugins.Steam.Authentication;
using Hatband.Plugins.Steam.Models;

namespace Hatband.Plugins.Steam.Abstractions;

/// <summary>
/// Provides an authenticated Steam player's profile and library.
/// </summary>
public interface ISteamPlayerService
{
    ConnectorAccount? CurrentAccount { get; }

    bool IsPersisted { get; }

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a QR-code login flow.
    /// </summary>
    Task<ISteamQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the games owned by the currently authenticated Steam account.
    /// </summary>
    Task<IReadOnlyList<SteamLibraryGame>> GetOwnedGamesAsync(CancellationToken cancellationToken = default);
}
