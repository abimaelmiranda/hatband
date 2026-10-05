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
    private readonly HttpClient httpClient;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();

    public SteamMetadataProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    public GameSourceId? SourceId => GameSourceId.Steam;

    public string ProviderId => "steam";

    public string DisplayName => "Steam";

    public bool CanSearch(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.SourceId == GameSourceId.Steam &&
               uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
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
        if (!CanSearch(game) || game.SourceGameId is not string sourceGameId)
        {
            return [];
        }

        var metadata = await GetMetadataForAppAsync(sourceGameId, languageTag, cancellationToken);
        return metadata is null ? [] : [metadata];
    }

    public Task<GameMetadata?> GetMetadataAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        if (!CanSearch(game) || game.SourceGameId is not string sourceGameId)
        {
            return Task.FromResult<GameMetadata?>(null);
        }

        return GetMetadataForAppAsync(sourceGameId, languageTag, cancellationToken);
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
        var response = await GetStoreDocumentAsync(appId, steamLanguage, cancellationToken);
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
