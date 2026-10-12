using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Plugins.Steam.Authentication;
using SteamKit2;
using SteamKit2.Authentication;

namespace Hatband.Plugins.Steam.Authentication;

internal sealed class SteamKitQrLoginSession : ISteamQrCodeLoginSession
{
    private readonly SteamKitConnection connection;
    private readonly SteamUser steamUser;
    private readonly QrAuthSession authSession;
    private readonly Func<SteamSession, CancellationToken, Task<bool>> onAuthenticated;
    private readonly Task<SteamUser.LoggedOnCallback> loginCompletion;
    private Task<ConnectorAccount>? authenticationTask;
    private bool disposed;

    private SteamKitQrLoginSession(
        SteamKitConnection connection,
        SteamUser steamUser,
        QrAuthSession authSession,
        Task<SteamUser.LoggedOnCallback> loginCompletion,
        Func<SteamSession, CancellationToken, Task<bool>> onAuthenticated)
    {
        this.connection = connection;
        this.steamUser = steamUser;
        this.authSession = authSession;
        this.loginCompletion = loginCompletion;
        this.onAuthenticated = onAuthenticated;
        ChallengeUri = new Uri(authSession.ChallengeURL, UriKind.Absolute);
        authSession.ChallengeURLChanged = OnChallengeUrlChanged;
    }

    public Uri ChallengeUri { get; private set; }

    public event EventHandler<SteamQrChallengeUriChangedEventArgs>? ChallengeUriChanged;

    public static async Task<SteamKitQrLoginSession> CreateAsync(
        SteamKitConnectionFactory connectionFactory,
        Func<SteamSession, CancellationToken, Task<bool>> onAuthenticated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(onAuthenticated);

        using var preparationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        preparationTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        var connection = await connectionFactory.ConnectAsync(preparationTimeout.Token);
        var steamClient = connection.Client;
        var steamUser = steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamKit did not provide the Steam user handler.");
        var callbackManager = connection.CallbackManager;

        var loginCompletion = new TaskCompletionSource<SteamUser.LoggedOnCallback>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(callback => loginCompletion.TrySetResult(callback));

        try
        {
            var authSessionTask = steamClient.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
            {
                DeviceFriendlyName = "Hatband",
                IsPersistentSession = true,
                WebsiteID = "Client",
            });
            var authSession = await WaitForConnectionAsync(
                connection,
                authSessionTask,
                preparationTimeout.Token,
                "Steam disconnected while preparing QR authentication.");

            var result = new SteamKitQrLoginSession(
                connection,
                steamUser,
                authSession,
                loginCompletion.Task,
                onAuthenticated);
            return result;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<ConnectorAccount> WaitForAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        authenticationTask ??= AuthenticateAsync(cancellationToken);
        return authenticationTask;
    }

    private async Task<ConnectorAccount> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var authResult = await WaitForConnectionAsync(
            connection,
            authSession.PollingWaitForResultAsync(cancellationToken),
            cancellationToken,
            "Steam disconnected while waiting for QR authentication.");
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = authResult.AccountName,
            AccessToken = authResult.RefreshToken,
            ShouldRememberPassword = true
        });

        var loginResult = await WaitForConnectionAsync(
            connection,
            loginCompletion,
            cancellationToken,
            "Steam disconnected while completing login.");
        if (loginResult.Result != EResult.OK)
        {
            throw new InvalidOperationException(
                $"Steam login failed: {loginResult.Result} / {loginResult.ExtendedResult}.");
        }

        if (string.IsNullOrWhiteSpace(authResult.AccessToken))
        {
            throw new InvalidOperationException("Steam did not return an access token for the authenticated account.");
        }

        if (loginResult.ClientSteamID is not SteamID authenticatedSteamId)
        {
            throw new InvalidOperationException("Steam login completed without an account identifier.");
        }

        var profile = new ConnectorAccount
        {
            SourceId = GameSourceIds.Steam,
            AccountId = authenticatedSteamId.ConvertToUInt64().ToString(),
            DisplayName = authResult.AccountName
        };
        var session = new SteamSession
        {
            Account = profile,
            AccessToken = authResult.AccessToken,
            RefreshToken = authResult.RefreshToken,
            AccessTokenExpiresAt = JwtTokenUtilities.GetExpiration(authResult.AccessToken)
        };
        session.IsPersisted = await onAuthenticated(session, cancellationToken);
        return profile;
    }

    private void OnChallengeUrlChanged()
    {
        ChallengeUri = new Uri(authSession.ChallengeURL, UriKind.Absolute);
        ChallengeUriChanged?.Invoke(this, new SteamQrChallengeUriChangedEventArgs(ChallengeUri));
    }

    private static async Task<T> WaitForConnectionAsync<T>(
        SteamKitConnection connection,
        Task<T> operation,
        CancellationToken cancellationToken,
        string disconnectMessage)
    {
        var cancellableOperation = operation.WaitAsync(cancellationToken);
        var completedTask = await Task.WhenAny(cancellableOperation, connection.Disconnected.Task);
        if (completedTask == connection.Disconnected.Task)
        {
            var disconnection = await connection.Disconnected.Task;
            throw new InvalidOperationException(
                $"{disconnectMessage} (user initiated: {disconnection.UserInitiated}).");
        }

        return await cancellableOperation;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        authSession.ChallengeURLChanged = null;
        await connection.DisposeAsync();
    }
}
