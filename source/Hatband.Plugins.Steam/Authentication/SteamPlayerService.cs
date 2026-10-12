using System.Net;
using System.Text.Json;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Plugins.Steam.Authentication;
using Hatband.Plugins.Steam.Abstractions;
using Hatband.Plugins.Steam.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.Plugins.Steam.Authentication;

public sealed class SteamPlayerService : ISteamPlayerService
{
    private const int CredentialsVersion = 1;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConnectorSessionStore sessionStore;
    private readonly ISteamTokenRefresher tokenRefresher;
    private readonly SteamKitConnectionFactory connectionFactory;
    private readonly ILogger<SteamPlayerService> logger;
    private readonly Lock authenticationLock = new();
    private readonly SemaphoreSlim sessionOperationGate = new(1, 1);
    private SteamSession? currentSession;

    public SteamPlayerService(
        IHttpClientFactory httpClientFactory,
        IConnectorSessionStore sessionStore,
        ISteamTokenRefresher tokenRefresher,
        SteamKitConnectionFactory connectionFactory,
        ILogger<SteamPlayerService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(tokenRefresher);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);
        this.httpClientFactory = httpClientFactory;
        this.sessionStore = sessionStore;
        this.tokenRefresher = tokenRefresher;
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

    private SteamSession? CurrentSession
    {
        get
        {
            lock (authenticationLock)
            {
                return currentSession;
            }
        }
    }

    public ConnectorAccount? CurrentAccount => CurrentSession?.Account;

    public bool IsPersisted => CurrentSession?.IsPersisted ?? false;

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            await sessionStore.DeleteAsync(GameSourceIds.Steam, cancellationToken);
            lock (authenticationLock)
            {
                currentSession = null;
            }
        }
        finally
        {
            sessionOperationGate.Release();
        }
    }

    public async Task<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            var storedCredentials = await sessionStore.LoadAsync<SteamCredentials>(GameSourceIds.Steam, cancellationToken);
            if (storedCredentials is null)
            {
                return false;
            }

            if (storedCredentials.Version != CredentialsVersion)
            {
                throw new InvalidDataException($"The saved Steam session version '{storedCredentials.Version}' is unsupported.");
            }

            var credentials = storedCredentials.Credentials;
            ValidateCredentials(credentials);
            var session = new SteamSession
            {
                Account = credentials.Account,
                AccessToken = credentials.AccessToken,
                RefreshToken = credentials.RefreshToken,
                AccessTokenExpiresAt = credentials.AccessTokenExpiresAt,
                IsPersisted = true
            };

            session.IsPersisted = true;
            lock (authenticationLock)
            {
                currentSession = session;
            }

            return true;
        }
        finally
        {
            sessionOperationGate.Release();
        }
    }

    public async Task<ISteamQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            lock (authenticationLock)
            {
                currentSession = null;
            }
        }
        finally
        {
            sessionOperationGate.Release();
        }

        return await SteamKitQrLoginSession.CreateAsync(
            connectionFactory,
            SetAuthenticatedSessionAsync,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SteamLibraryGame>> GetOwnedGamesAsync(CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        using var httpClient = httpClientFactory.CreateClient("Hatband.Plugin.Steam.Player");
        var response = await GetOwnedGamesResponseAsync(
            httpClient,
            session.Account.AccountId,
            session.AccessToken,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            session = await ForceRefreshAsync(cancellationToken);
            response = await GetOwnedGamesResponseAsync(
                httpClient,
                session.Account.AccountId,
                session.AccessToken,
                cancellationToken);
        }

        return await ReadOwnedGamesAsync(response, cancellationToken);
    }

    private static async Task<IReadOnlyList<SteamLibraryGame>> ReadOwnedGamesAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SteamOwnedGamesEnvelope>(
                responseStream,
                cancellationToken: cancellationToken);
            if (payload?.Response is null)
            {
                throw new InvalidOperationException("Steam returned an invalid response while loading the game library.");
            }

            var library = payload.Response;
            if (library.GameCount is null)
            {
                throw new InvalidOperationException("Steam's library response did not include a game count.");
            }

            if (library.Games is null)
            {
                throw new InvalidOperationException("Steam's library response contained a null game list.");
            }

            var ownedGames = library.Games;
            if (library.GameCount > 0 && ownedGames.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Steam reported {library.GameCount} owned games but did not include the game list. Check the account's game privacy settings and try again.");
            }

            var games = new List<SteamLibraryGame>();
            foreach (var game in ownedGames)
            {
                var name = game.Name;
                if (game.AppId == 0 || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                games.Add(new SteamLibraryGame
                {
                    AppId = game.AppId,
                    Name = name.Trim(),
                    PlaytimeSeconds = (long)game.PlaytimeForever * 60,
                    LastActivity = ToUtcDateTime(game.LastPlayedUnix)
                });
            }

            return games;
        }
    }

    private async Task<bool> SetAuthenticatedSessionAsync(SteamSession session, CancellationToken cancellationToken)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            session.IsPersisted = false;
            lock (authenticationLock)
            {
                currentSession = session;
            }

            try
            {
                await sessionStore.SaveAsync(
                    GameSourceIds.Steam,
                    CredentialsVersion,
                    new SteamCredentials
                    {
                        Account = session.Account,
                        AccessToken = session.AccessToken,
                        RefreshToken = session.RefreshToken,
                        AccessTokenExpiresAt = session.AccessTokenExpiresAt
                    },
                    cancellationToken);
                session.IsPersisted = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lock (authenticationLock)
                {
                    if (ReferenceEquals(currentSession, session))
                    {
                        currentSession = null;
                    }
                }

                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Steam session is active in memory but could not be saved securely.");
                return false;
            }
        }
        finally
        {
            sessionOperationGate.Release();
        }
    }

    private async Task<SteamSession> GetCurrentSessionAsync(CancellationToken cancellationToken)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            var session = CurrentSession
                ?? throw new InvalidOperationException("Connect a Steam account before loading its library.");
            if (session.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return session;
            }

            return await RefreshSessionAsync(session, cancellationToken);
        }
        finally
        {
            sessionOperationGate.Release();
        }
    }

    private async Task<SteamSession> ForceRefreshAsync(CancellationToken cancellationToken)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            var session = CurrentSession
                ?? throw new InvalidOperationException("Connect a Steam account before loading its library.");
            return await RefreshSessionAsync(session, cancellationToken);
        }
        finally
        {
            sessionOperationGate.Release();
        }
    }

    private async Task<SteamSession> RefreshSessionAsync(
        SteamSession session,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(session.Account.AccountId, out var steamId))
        {
            throw new InvalidOperationException("The saved Steam account identifier is invalid.");
        }

        var refreshed = await tokenRefresher.RefreshAsync(steamId, session.RefreshToken, cancellationToken);
        var updatedSession = new SteamSession
        {
            Account = session.Account,
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken ?? session.RefreshToken,
            AccessTokenExpiresAt = JwtTokenUtilities.GetExpiration(refreshed.AccessToken),
            IsPersisted = session.IsPersisted
        };

        lock (authenticationLock)
        {
            if (ReferenceEquals(currentSession, session))
            {
                currentSession = updatedSession;
            }
        }

        try
        {
            await sessionStore.SaveAsync(
                GameSourceIds.Steam,
                CredentialsVersion,
                new SteamCredentials
                {
                    Account = updatedSession.Account,
                    AccessToken = updatedSession.AccessToken,
                    RefreshToken = updatedSession.RefreshToken,
                    AccessTokenExpiresAt = updatedSession.AccessTokenExpiresAt
                },
                cancellationToken);
            updatedSession.IsPersisted = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            updatedSession.IsPersisted = false;
            logger.LogWarning(exception, "The renewed Steam session could not be saved securely.");
        }

        return updatedSession;
    }

    private static void ValidateCredentials(SteamCredentials credentials)
    {
        if (credentials.Account is null || credentials.Account.SourceId != GameSourceIds.Steam ||
            string.IsNullOrWhiteSpace(credentials.Account.AccountId) ||
            string.IsNullOrWhiteSpace(credentials.AccessToken) ||
            string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            throw new InvalidDataException("The saved Steam session is incomplete or belongs to another connector.");
        }
    }

    private static async Task<HttpResponseMessage> GetOwnedGamesResponseAsync(
        HttpClient httpClient,
        string steamId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["format"] = "json",
            ["access_token"] = accessToken,
            ["steamid"] = steamId,
            ["include_appinfo"] = "true",
            ["include_played_free_games"] = "true",
            ["include_free_sub"] = "true",
            ["language"] = "english"
        };
        var queryString = string.Join(
            "&",
            query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        var requestUri = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?{queryString}";

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var shouldRetry = response.StatusCode == HttpStatusCode.TooManyRequests ||
                              (int)response.StatusCode >= 500;
            if (shouldRetry && attempt < 4)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 3);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Steam did not return a library response after several attempts.");
    }

    private static DateTime? ToUtcDateTime(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
