using Hatband.Core.Abstractions;
using Hatband.Core.Models;

namespace Hatband.Integrations.IGN;

/// <summary>
/// Performs explicit metadata searches against IGN and is excluded from automatic enrichment.
/// </summary>
public sealed class IgnMetadataSearchProvider : IGameMetadataSearchProvider
{
    private readonly IgnGraphQlClient client;

    public IgnMetadataSearchProvider(HttpClient httpClient)
    {
        client = new IgnGraphQlClient(httpClient);
    }

    public string ProviderId => "ign";

    public string DisplayName => "IGN";

    public bool CanSearch(GameSourceLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !string.IsNullOrWhiteSpace(request.GameName);
    }

    /// <summary>
    /// Searches IGN by game title. IGN's current searchable metadata is English-only.
    /// </summary>
    public async Task<GameMetadataSearchResponse> SearchAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GameName);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var result = await client.SearchGamesAsync(
            request.GameName,
            region,
            preferredLanguageTag,
            cancellationToken);
        if (result.ErrorMessage is not null)
        {
            return new GameMetadataSearchResponse { ErrorMessage = result.ErrorMessage };
        }

        var searchEntries = result.Value
            ?? throw new InvalidOperationException("IGN returned a successful search without results.");
        var results = searchEntries.Select(entry => new GameMetadataSearchResult
        {
            Id = entry.Id,
            Name = entry.Name,
            ProviderToken = entry.Slug,
            ReleaseDate = entry.ReleaseDate,
            Platforms = entry.Platforms,
            PrimaryImageUrl = entry.PrimaryImageUrl
        }).ToArray();
        return new GameMetadataSearchResponse { Results = results };
    }

    public async Task<GameMetadataLookupResponse> GetDetailsAsync(
        GameMetadataSearchResult selection,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.ProviderToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var result = await client.GetGameDetailsAsync(
            selection.ProviderToken,
            region,
            preferredLanguageTag,
            cancellationToken);
        if (result.ErrorMessage is not null)
        {
            return new GameMetadataLookupResponse { ErrorMessage = result.ErrorMessage };
        }

        var details = result.Value
            ?? throw new InvalidOperationException("IGN returned successful details without metadata.");
        var metadata = new GameMetadata
        {
            LanguageTag = "en",
            Description = details.Description,
            Developer = details.Developer,
            Publisher = details.Publisher,
            Genre = details.Genre,
            ReleaseDate = details.ReleaseDate
        };

        return new GameMetadataLookupResponse
        {
            Metadata = metadata,
            RequestedLanguageTag = preferredLanguageTag,
            ContentLanguageTag = "en",
            IsContentLanguageMatch = LanguageMatches(preferredLanguageTag, "en"),
            SourceUri = new Uri($"https://www.ign.com/games/{Uri.EscapeDataString(selection.ProviderToken)}")
        };
    }

    private static bool LanguageMatches(string requestedLanguageTag, string contentLanguageTag)
    {
        var requestedLanguage = requestedLanguageTag.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(requestedLanguage, contentLanguageTag, StringComparison.OrdinalIgnoreCase);
    }
}
