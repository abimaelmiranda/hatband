namespace Hatband.Integrations.Steam.Abstractions;

public interface ISteamTokenRefresher
{
    Task<(string AccessToken, string? RefreshToken)> RefreshAsync(
        ulong steamId,
        string refreshToken,
        CancellationToken cancellationToken = default);
}
