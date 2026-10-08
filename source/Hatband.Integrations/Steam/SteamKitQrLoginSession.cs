using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Integrations.Authentication;
using SteamKit2;
using SteamKit2.Authentication;

namespace Hatband.Integrations.Steam;

internal sealed class SteamKitQrLoginSession : IQrCodeLoginSession
{
    private readonly SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly QrAuthSession authSession;
    private readonly Task callbackPump;
    private readonly CancellationTokenSource callbackPumpCancellation;
    private readonly Func<ConnectorSession, CancellationToken, Task<bool>> onAuthenticated;
    private readonly Task<SteamUser.LoggedOnCallback> loginCompletion;
    private Task<ConnectorSession>? authenticationTask;
    private bool disposed;

    private SteamKitQrLoginSession(
        SteamClient steamClient,
        SteamUser steamUser,
        QrAuthSession authSession,
        Task<SteamUser.LoggedOnCallback> loginCompletion,
        Task callbackPump,
        CancellationTokenSource callbackPumpCancellation,
        Func<ConnectorSession, CancellationToken, Task<bool>> onAuthenticated)
    {
        this.steamClient = steamClient;
        this.steamUser = steamUser;
        this.authSession = authSession;
        this.loginCompletion = loginCompletion;
        this.callbackPump = callbackPump;
        this.callbackPumpCancellation = callbackPumpCancellation;
        this.onAuthenticated = onAuthenticated;
        ChallengeUri = new Uri(authSession.ChallengeURL, UriKind.Absolute);
        authSession.ChallengeURLChanged = OnChallengeUrlChanged;
    }

    public Uri ChallengeUri { get; private set; }

    public event EventHandler<QrChallengeUriChangedEventArgs>? ChallengeUriChanged;

    public static async Task<SteamKitQrLoginSession> CreateAsync(
        Func<ConnectorSession, CancellationToken, Task<bool>> onAuthenticated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onAuthenticated);

        var steamClient = new SteamClient();
        var steamUser = steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamKit did not provide the Steam user handler.");
        var callbackManager = new CallbackManager(steamClient);
        var connectionCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackPumpCancellation = new CancellationTokenSource();

        callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ => connectionCompletion.TrySetResult());
        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(callback =>
        {
            if (!callback.UserInitiated)
            {
                connectionCompletion.TrySetException(
                    new InvalidOperationException("Steam disconnected while starting authentication."));
            }
        });

        var loginCompletion = new TaskCompletionSource<SteamUser.LoggedOnCallback>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(callback => loginCompletion.TrySetResult(callback));

        var callbackPump = Task.Run(() => RunCallbackPump(callbackManager, callbackPumpCancellation.Token));

        try
        {
            steamClient.Connect();
            await connectionCompletion.Task.WaitAsync(cancellationToken);

            var authSession = await steamClient.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
            {
                DeviceFriendlyName = "Hatband",
                IsPersistentSession = true,
                WebsiteID = "Client",
            });

            var result = new SteamKitQrLoginSession(
                steamClient,
                steamUser,
                authSession,
                loginCompletion.Task,
                callbackPump,
                callbackPumpCancellation,
                onAuthenticated);
            return result;
        }
        catch
        {
            callbackPumpCancellation.Cancel();
            steamClient.Disconnect();
            await callbackPump;
            callbackPumpCancellation.Dispose();
            throw;
        }
    }

    public Task<ConnectorSession> WaitForAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        authenticationTask ??= AuthenticateAsync(cancellationToken);
        return authenticationTask;
    }

    private async Task<ConnectorSession> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var authResult = await authSession.PollingWaitForResultAsync(cancellationToken);
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = authResult.AccountName,
            AccessToken = authResult.RefreshToken,
            ShouldRememberPassword = true
        });

        var loginResult = await loginCompletion.WaitAsync(cancellationToken);
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
            SourceId = GameSourceId.Steam,
            AccountId = authenticatedSteamId.ConvertToUInt64().ToString(),
            DisplayName = authResult.AccountName
        };
        var session = new ConnectorSession
        {
            SourceId = GameSourceId.Steam,
            Profile = profile,
            AccessToken = authResult.AccessToken,
            RefreshToken = authResult.RefreshToken,
            AccessTokenExpiresAt = JwtTokenUtilities.GetExpiration(authResult.AccessToken)
        };
        session.IsPersisted = await onAuthenticated(session, cancellationToken);
        return session;
    }

    private void OnChallengeUrlChanged()
    {
        ChallengeUri = new Uri(authSession.ChallengeURL, UriKind.Absolute);
        ChallengeUriChanged?.Invoke(this, new QrChallengeUriChangedEventArgs(ChallengeUri));
    }

    internal static void RunCallbackPump(CallbackManager callbackManager, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        authSession.ChallengeURLChanged = null;
        callbackPumpCancellation.Cancel();
        steamClient.Disconnect();
        await callbackPump;
        callbackPumpCancellation.Dispose();
    }
}
