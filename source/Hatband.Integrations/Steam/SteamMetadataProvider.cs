using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Integrations.Steam.Models;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Reads descriptive metadata from the Steam store.
/// </summary>
public sealed partial class SteamMetadataProvider : IGameMetadataProvider
{
    private const int MaximumNameSearchResults = 5;
    private readonly IHttpClientFactory _httpClientFactory;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(
        "<a\\b(?=[^>]*\\bclass\\s*=\\s*['\"][^'\"]*search_result_row[^'\"]*['\"])[^>]*>.*?</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SearchResultRowPattern();

    [GeneratedRegex("\\bdata-ds-appid\\s*=\\s*['\"](?<appId>\\d+)['\"]", RegexOptions.IgnoreCase)]
    private static partial Regex SearchResultAppIdPattern();

    [GeneratedRegex("\\bdata-ds-packageid\\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex SearchResultPackageIdPattern();

    public SteamMetadataProvider(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    public GameSourceId? SourceId => GameSourceId.Steam;

    public bool SupportsManualSearch => true;

    public string ProviderId => "steam";

    public string DisplayName => "Steam";

    public bool CanSearch(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.SourceId switch
        {
            GameSourceId.Steam => uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out _),
            GameSourceId.Manual => !string.IsNullOrWhiteSpace(game.Name),
            _ => false
        };
    }

    public async Task<IReadOnlyList<GameMetadata>> SearchAsync(
        Game game,
        string languageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if (game.SourceId == GameSourceId.Manual)
        {
            return await SearchByNameAsync(game.Name, languageTag, cancellationToken);
        }

        if (!CanSearch(game) || game.SourceGameId is not string sourceGameId)
        {
            return [];
        }

        var metadata = await GetMetadataForAppAsync(sourceGameId, languageTag, cancellationToken);
        return metadata is null ? [] : [metadata];
    }

    public async Task<GameMetadata?> GetMetadataAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        if (game.SourceId == GameSourceId.Manual)
        {
            if (game.Metadata.StoreSourceId == GameSourceId.Steam &&
                game.Metadata.StoreGameId is { } storedAppId &&
                uint.TryParse(storedAppId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return await GetMetadataForAppAsync(storedAppId, languageTag, cancellationToken);
            }

            if (!CanSearch(game))
            {
                return null;
            }

            var matches = await SearchByNameAsync(game.Name, languageTag, cancellationToken);
            return matches.FirstOrDefault();
        }

        if (!CanSearch(game) || game.SourceGameId is not string sourceGameId)
        {
            return null;
        }

        return await GetMetadataForAppAsync(sourceGameId, languageTag, cancellationToken);
    }

    private async Task<IReadOnlyList<GameMetadata>> SearchByNameAsync(
        string gameName,
        string languageTag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        var appIds = await SearchAppIdsAsync(gameName, cancellationToken);
        var results = new List<GameMetadata>(Math.Min(appIds.Count, MaximumNameSearchResults));
        foreach (var appId in appIds.Take(MaximumNameSearchResults))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = await GetMetadataForAppAsync(appId.ToString(CultureInfo.InvariantCulture), languageTag, cancellationToken);
            if (metadata is not null)
            {
                results.Add(metadata);
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<uint>> SearchAppIdsAsync(
        string gameName,
        CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var requestUri = $"https://store.steampowered.com/search/?term={Uri.EscapeDataString(gameName)}" +
                         "&ignore_preferences=1&category1=998&ndl=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd("Hatband/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var appIds = new List<uint>();
        foreach (Match row in SearchResultRowPattern().Matches(html))
        {
            if (SearchResultPackageIdPattern().IsMatch(row.Value))
            {
                continue;
            }

            var appIdMatch = SearchResultAppIdPattern().Match(row.Value);
            if (!appIdMatch.Success ||
                !uint.TryParse(appIdMatch.Groups["appId"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
            {
                continue;
            }

            appIds.Add(appId);
        }

        return appIds.Distinct().ToArray();
    }

    private async Task<GameMetadata?> GetMetadataForAppAsync(
        string sourceGameId,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGameId);
        if (!uint.TryParse(sourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            throw new ArgumentException("Steam game IDs must be numeric app IDs.", nameof(sourceGameId));
        }

        var (steamLanguage, contentLanguageTag) = SteamLanguage.Resolve(languageTag);
        var dateCulture = CultureInfo.GetCultureInfo(languageTag);
        using var httpClient = _httpClientFactory.CreateClient();
        var response = await GetStoreDocumentAsync(httpClient, appId, steamLanguage, cancellationToken);
        if (response is null ||
            !response.TryGetValue(appId.ToString(CultureInfo.InvariantCulture), out var appResult) ||
            !appResult.Success ||
            appResult.Data is not SteamAppDetails data)
        {
            return null;
        }

        var dateText = data.ReleaseDate?.Date;
        DateOnly? releaseDate = null;
        if (DateOnly.TryParse(dateText, dateCulture, DateTimeStyles.None, out var parsedLocalizedDate))
        {
            releaseDate = parsedLocalizedDate;
        }
        else if (DateOnly.TryParse(
                     dateText,
                     CultureInfo.GetCultureInfo("en-US"),
                     DateTimeStyles.None,
                     out var parsedEnglishDate))
        {
            releaseDate = parsedEnglishDate;
        }

        return new GameMetadata
        {
            LanguageTag = contentLanguageTag,
            StoreSourceId = GameSourceId.Steam,
            StoreGameId = appId.ToString(CultureInfo.InvariantCulture),
            StoreName = NullIfWhiteSpace(data.Name),
            Description = GetDescription(data),
            Developer = JoinArray(data.Developers),
            Publisher = JoinArray(data.Publishers),
            Genre = JoinGenres(data.Genres),
            ReleaseDate = releaseDate,
            NativePlatforms = GetNativePlatforms(data.Platforms)
        };
    }

    private async Task<Dictionary<string, SteamAppDetailsResult>?> GetStoreDocumentAsync(
        HttpClient httpClient,
        uint appId,
        string steamLanguage,
        CancellationToken cancellationToken)
    {
        var requestUri = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={Uri.EscapeDataString(steamLanguage)}";
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == 4)
                {
                    response.EnsureSuccessStatusCode();
                }

                var retryDelay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 2);
                await Task.Delay(retryDelay, cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync(
                contentStream,
                SteamJsonSerializerContext.Default.DictionaryStringSteamAppDetailsResult,
                cancellationToken);
        }

        return null;
    }

    private static string? GetDescription(SteamAppDetails data)
    {
        var html = data.AboutTheGame;
        if (string.IsNullOrWhiteSpace(html))
        {
            html = data.ShortDescription;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var plainText = WebUtility.HtmlDecode(HtmlTagPattern().Replace(html, " "));
        return WhitespacePattern().Replace(plainText, " ").Trim();
    }

    private static GamePlatform? GetNativePlatforms(SteamPlatformSupport? platforms)
    {
        if (platforms is null)
        {
            return null;
        }

        if (platforms.Windows is null && platforms.MacOS is null && platforms.Linux is null)
        {
            return null;
        }

        var nativePlatforms = GamePlatform.None;
        if (platforms.Windows == true)
        {
            nativePlatforms |= GamePlatform.Windows;
        }

        if (platforms.MacOS == true)
        {
            nativePlatforms |= GamePlatform.MacOS;
        }

        if (platforms.Linux == true)
        {
            nativePlatforms |= GamePlatform.Linux;
        }

        return nativePlatforms;
    }

    private static string? JoinArray(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var names = values
            .Select(NullIfWhiteSpace)
            .OfType<string>()
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static string? JoinGenres(IEnumerable<SteamGenre>? genres)
    {
        if (genres is null)
        {
            return null;
        }

        var names = genres
            .Select(genre => NullIfWhiteSpace(genre.Description))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
