using System.Net;
using System.Text.RegularExpressions;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Extensions;
using Hatband.Core.Models.Games;

namespace Hatband.Integrations.IGN;

/// <summary>
/// Searches IGN by game title and loads metadata for the matching entries.
/// </summary>
public sealed partial class IgnMetadataProvider : IGameMetadataProvider
{
    private const string DefaultRegion = "US";
    private const int MaximumDetailedResults = 5;

    private readonly IgnGraphQlClient client;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();

    public IgnMetadataProvider(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        client = new IgnGraphQlClient(httpClientFactory);
    }

    public string ProviderId => "ign";

    public string DisplayName => "IGN";

    public GameSourceId? SourceId => null;

    public bool SupportsManualSearch => true;

    public bool CanSearch(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return !string.IsNullOrWhiteSpace(game.Name);
    }

    /// <summary>
    /// Searches IGN by title. IGN's current metadata is English-only.
    /// </summary>
    public async Task<IReadOnlyList<GameMetadata>> SearchAsync(
        Game game,
        string languageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var searchResult = await client.SearchGamesAsync(
            game.Name,
            region,
            languageTag,
            cancellationToken);
        if (searchResult.ErrorMessage is not null)
        {
            return [];
        }

        var entries = searchResult.Value
            ?? throw new InvalidOperationException("IGN returned a successful search without results.");
        var metadataResults = new List<GameMetadata>(Math.Min(entries.Count, MaximumDetailedResults));
        foreach (var entry in entries.Take(MaximumDetailedResults))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detailsResult = await client.GetGameDetailsAsync(
                entry.Slug,
                region,
                languageTag,
                cancellationToken);
            if (detailsResult.ErrorMessage is not null)
            {
                continue;
            }

            var details = detailsResult.Value
                ?? throw new InvalidOperationException("IGN returned successful details without metadata.");
            var releaseDate = details.ReleaseDate;
            if (releaseDate is null && entry.ReleaseDate.TryParseIsoDate(out var parsedReleaseDate))
            {
                releaseDate = parsedReleaseDate;
            }

            metadataResults.Add(new GameMetadata
            {
                LanguageTag = "en",
                StoreName = entry.Name,
                Description = CleanDescription(details.Description),
                Developer = details.Developer,
                Publisher = details.Publisher,
                Genre = details.Genre,
                ReleaseDate = releaseDate
            });
        }

        return metadataResults;
    }

    public async Task<GameMetadata?> GetMetadataAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(game, languageTag, DefaultRegion, cancellationToken);
        return results.FirstOrDefault();
    }

    private static string? CleanDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var plainText = WebUtility.HtmlDecode(HtmlTagPattern().Replace(description, " "));
        return WhitespacePattern().Replace(plainText, " ").Trim();
    }
}
