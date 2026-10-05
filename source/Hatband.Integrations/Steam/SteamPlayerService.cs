using System.Net;
using System.Text.Json;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Models.Authentication;
using Hatband.Core.Enums.Stores;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam;

public sealed class SteamPlayerService : ISteamPlayerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Lock authenticationLock = new();
    private string? steamId64;
    private string? accessToken;
    private ConnectorAccount? currentAccount;

    public SteamPlayerService(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.httpClientFactory = httpClientFactory;
    }

    public ConnectorAccount? CurrentAccount
    {
        get
        {
            lock (authenticationLock)
            {
                return currentAccount;
            }
        }
    }

    public void Disconnect()
    {
        lock (authenticationLock)
        {
            accessToken = null;
            steamId64 = null;
            currentAccount = null;
        }
    }

    public async Task<IQrCodeLoginSession> BeginQrLoginAsync(CancellationToken cancellationToken = default)
    {
        lock (authenticationLock)
        {
            accessToken = null;
            steamId64 = null;
            currentAccount = null;
        }

        return await SteamKitQrLoginSession.CreateAsync(SetAuthenticatedAccount, cancellationToken);
    }

    public async Task<IReadOnlyList<SteamLibraryGame>> GetOwnedGamesAsync(CancellationToken cancellationToken = default)
    {
        string activeSteamId;
        string activeAccessToken;
        lock (authenticationLock)
        {
            activeSteamId = steamId64 ?? throw new InvalidOperationException("Connect a Steam account before loading its library.");
            activeAccessToken = accessToken ?? throw new InvalidOperationException("Steam authentication did not provide an access token.");
        }

        using var httpClient = httpClientFactory.CreateClient("SteamPlayer");
        using var response = await GetOwnedGamesResponseAsync(
            httpClient,
            activeSteamId,
            activeAccessToken,
            cancellationToken);
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

    private void SetAuthenticatedAccount(ConnectorAccount account, string accountAccessToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.AccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountAccessToken);

        lock (authenticationLock)
        {
            steamId64 = account.AccountId;
            accessToken = accountAccessToken;
            currentAccount = account;
        }
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
