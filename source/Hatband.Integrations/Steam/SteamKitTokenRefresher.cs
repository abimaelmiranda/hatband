using SteamKit2;
using SteamKit2.Authentication;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Integrations.Steam;

public sealed class SteamKitTokenRefresher : ISteamTokenRefresher
{
    public async Task<(string AccessToken, string? RefreshToken)> RefreshAsync(
        ulong steamId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var client = new SteamClient();
        var callbackManager = new CallbackManager(client);
        using var pumpCancellation = new CancellationTokenSource();
        using var refreshTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        refreshTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ => connected.TrySetResult());
        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(callback =>
        {
            if (!callback.UserInitiated)
            {
                var exception = new InvalidOperationException("Steam disconnected while refreshing authentication.");
                connected.TrySetException(exception);
            }
        });

        var callbackPump = Task.Run(() => SteamKitQrLoginSession.RunCallbackPump(callbackManager, pumpCancellation.Token));
        try
        {
            client.Connect();
            await connected.Task.WaitAsync(refreshTimeout.Token);
            var tokenResult = await client.Authentication.GenerateAccessTokenForAppAsync(
                new SteamID(steamId),
                refreshToken,
                allowRenewal: true).WaitAsync(refreshTimeout.Token);
            if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            {
                throw new InvalidOperationException("Steam did not return an access token while refreshing authentication.");
            }

            return (tokenResult.AccessToken, string.IsNullOrWhiteSpace(tokenResult.RefreshToken)
                ? null
                : tokenResult.RefreshToken);
        }
        finally
        {
            pumpCancellation.Cancel();
            client.Disconnect();
            await callbackPump;
        }
    }
}
