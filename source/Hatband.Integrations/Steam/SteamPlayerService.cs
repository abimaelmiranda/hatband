using System.Net;
using System.Text.Json;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Integrations.Authentication;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.Integrations.Steam;

public sealed class SteamPlayerService : ISteamPlayerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConnectorSessionStore sessionStore;
    private readonly ISteamTokenRefresher tokenRefresher;
    private readonly ILogger<SteamPlayerService> logger;
    private readonly Lock authenticationLock = new();
    private readonly SemaphoreSlim sessionOperationGate = new(1, 1);
    private ConnectorSession? currentSession;

    public SteamPlayerService(
        IHttpClientFactory httpClientFactory,
        IConnectorSessionStore sessionStore,
        ISteamTokenRefresher tokenRefresher,
        ILogger<SteamPlayerService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(tokenRefresher);
        ArgumentNullException.ThrowIfNull(logger);
        this.httpClientFactory = httpClientFactory;
        this.sessionStore = sessionStore;
        this.tokenRefresher = tokenRefresher;
        this.logger = logger;
    }

    public ConnectorSession? CurrentSession
    {
        get
        {
            lock (authenticationLock)
            {
                return currentSession;
            }
        }
    }

    public ConnectorAccount? CurrentAccount => CurrentSession?.Profile;

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await sessionOperationGate.WaitAsync(cancellationToken);
        try
        {
            await sessionStore.DeleteAsync(GameSourceId.Steam, cancellationToken);
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
            var session = await sessionStore.LoadAsync(GameSourceId.Steam, cancellationToken);
            if (session is null)
            {
                return false;
            }

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

    public async Task<IQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default)
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

        return await SteamKitQrLoginSession.CreateAsync(SetAuthenticatedSessionAsync, cancellationToken);
    }

    public async Task<IReadOnlyList<SteamLibraryGame>> GetOwnedGamesAsync(CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        using var httpClient = httpClientFactory.CreateClient("SteamPlayer");
        var response = await GetOwnedGamesResponseAsync(
            httpClient,
            session.Profile.AccountId,
            session.AccessToken,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            session = await ForceRefreshAsync(cancellationToken);
            response = await GetOwnedGamesResponseAsync(
                httpClient,
                session.Profile.AccountId,
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

    private async Task<bool> SetAuthenticatedSessionAsync(ConnectorSession session, CancellationToken cancellationToken)
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
                await sessionStore.SaveAsync(session, cancellationToken);
                session.IsPersisted = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
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

    private async Task<ConnectorSession> GetCurrentSessionAsync(CancellationToken cancellationToken)
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

    private async Task<ConnectorSession> ForceRefreshAsync(CancellationToken cancellationToken)
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

    private async Task<ConnectorSession> RefreshSessionAsync(
        ConnectorSession session,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(session.Profile.AccountId, out var steamId))
        {
            throw new InvalidOperationException("The saved Steam account identifier is invalid.");
        }

        var refreshed = await tokenRefresher.RefreshAsync(steamId, session.RefreshToken, cancellationToken);
        var updatedSession = new ConnectorSession
        {
            SourceId = session.SourceId,
            Profile = session.Profile,
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
            await sessionStore.SaveAsync(updatedSession, cancellationToken);
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
