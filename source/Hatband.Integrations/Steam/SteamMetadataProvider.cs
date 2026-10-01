using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Reads descriptive metadata from the Steam store.
/// </summary>
public sealed partial class SteamMetadataProvider : IGameMetadataProvider, IGameMetadataSearchProvider
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

    public GameSourceId SourceId => GameSourceId.Steam;

    public string ProviderId => "steam";

    public string DisplayName => "Steam";

    public bool CanSearch(GameSourceLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.SourceId == GameSourceId.Steam &&
               uint.TryParse(request.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    public Task<GameMetadataSearchResponse> SearchAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanSearch(request))
        {
            return Task.FromResult(new GameMetadataSearchResponse
            {
                ErrorMessage = "A Steam app ID is required to load metadata from Steam."
            });
        }

        if (string.IsNullOrWhiteSpace(request.SourceGameId))
        {
            throw new InvalidOperationException("A searchable Steam request must contain a Steam app ID.");
        }

        var appId = request.SourceGameId;
        return Task.FromResult(new GameMetadataSearchResponse
        {
            Results =
            [
                new GameMetadataSearchResult
                {
                    Id = appId,
                    Name = request.GameName,
                    ProviderToken = appId
                }
            ]
        });
    }

    public async Task<GameMetadataLookupResponse> GetDetailsAsync(
        GameMetadataSearchResult selection,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var (_, contentLanguageTag) = SteamLanguage.Resolve(preferredLanguageTag);
        var metadata = await GetMetadataAsync(selection.ProviderToken, preferredLanguageTag, cancellationToken);
        if (metadata is null)
        {
            return new GameMetadataLookupResponse
            {
                ErrorMessage = "Steam did not return metadata for this game."
            };
        }

        return new GameMetadataLookupResponse
        {
            Metadata = metadata,
            RequestedLanguageTag = preferredLanguageTag,
            ContentLanguageTag = contentLanguageTag,
            IsContentLanguageMatch = LanguageMatches(preferredLanguageTag, contentLanguageTag),
            SourceUri = new Uri($"https://store.steampowered.com/app/{Uri.EscapeDataString(selection.ProviderToken)}")
        };
    }

    public async Task<GameMetadata?> GetMetadataAsync(
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
        using var document = await GetStoreDocumentAsync(appId, steamLanguage, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var appResult) ||
            !appResult.TryGetProperty("success", out var success) ||
            !success.GetBoolean() ||
            !appResult.TryGetProperty("data", out var data))
        {
            return null;
        }

        var dateText = GetNestedString(data, "release_date", "date");
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
            StoreName = GetString(data, "name"),
            Description = GetDescription(data),
            Developer = JoinArray(data, "developers"),
            Publisher = JoinArray(data, "publishers"),
            Genre = JoinNestedArray(data, "genres", "description"),
            ReleaseDate = releaseDate
        };
    }

    private async Task<JsonDocument?> GetStoreDocumentAsync(
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
            return await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
        }

        return null;
    }

    private static string? GetDescription(JsonElement data)
    {
        var html = GetString(data, "about_the_game");
        if (string.IsNullOrWhiteSpace(html))
        {
            html = GetString(data, "short_description");
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var plainText = WebUtility.HtmlDecode(HtmlTagPattern().Replace(html, " "));
        return WhitespacePattern().Replace(plainText, " ").Trim();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? NullIfWhiteSpace(value.GetString())
            : null;
    }

    private static string? GetNestedString(JsonElement element, string parentName, string propertyName)
    {
        return element.TryGetProperty(parentName, out var parent)
            ? GetString(parent, propertyName)
            : null;
    }

    private static string? JoinArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .OfType<string>()
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static string? JoinNestedArray(JsonElement element, string propertyName, string valueName)
    {
        if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = values.EnumerateArray()
            .Select(value => GetString(value, valueName))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool LanguageMatches(string requestedLanguageTag, string contentLanguageTag)
    {
        var requestedLanguage = CultureInfo.GetCultureInfo(requestedLanguageTag).TwoLetterISOLanguageName;
        var contentLanguage = CultureInfo.GetCultureInfo(contentLanguageTag).TwoLetterISOLanguageName;
        return string.Equals(requestedLanguage, contentLanguage, StringComparison.OrdinalIgnoreCase);
    }
}
