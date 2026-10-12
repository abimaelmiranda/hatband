using SteamKit2.Authentication;
using SteamKit2;
using Hatband.Plugins.Steam.Abstractions;

namespace Hatband.Plugins.Steam.Authentication;

public sealed class SteamKitTokenRefresher : ISteamTokenRefresher
{
    private readonly SteamKitConnectionFactory connectionFactory;

    public SteamKitTokenRefresher(SteamKitConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    public async Task<(string AccessToken, string? RefreshToken)> RefreshAsync(
        ulong steamId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        using var refreshTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        refreshTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        await using var connection = await connectionFactory.ConnectAsync(refreshTimeout.Token);
        var refreshTask = connection.Client.Authentication.GenerateAccessTokenForAppAsync(
            new SteamID(steamId),
            refreshToken,
            allowRenewal: true);
        var completedTask = await Task.WhenAny(refreshTask, connection.Disconnected.Task);
        if (completedTask == connection.Disconnected.Task)
        {
            var disconnection = await connection.Disconnected.Task;
            throw new InvalidOperationException(
                $"Steam disconnected while refreshing authentication (user initiated: {disconnection.UserInitiated}).");
        }

        var tokenResult = await refreshTask.WaitAsync(refreshTimeout.Token);
        if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            throw new InvalidOperationException("Steam did not return an access token while refreshing authentication.");
        }

        return (tokenResult.AccessToken, string.IsNullOrWhiteSpace(tokenResult.RefreshToken)
            ? null
            : tokenResult.RefreshToken);
    }
}
