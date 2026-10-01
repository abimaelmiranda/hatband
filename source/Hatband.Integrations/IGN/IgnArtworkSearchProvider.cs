using Hatband.Core.Abstractions;
using Hatband.Core.Models;

namespace Hatband.Integrations.IGN;

/// <summary>
/// Searches IGN's primary artwork and image gallery independently of metadata editing.
/// </summary>
public sealed class IgnArtworkSearchProvider : IGameArtworkSearchProvider
{
    private readonly IgnGraphQlClient client;

    public IgnArtworkSearchProvider(HttpClient httpClient)
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

    public async Task<GameArtworkSearchResponse> SearchArtworkAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GameName);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var search = await client.SearchGamesAsync(
            request.GameName,
            region,
            preferredLanguageTag,
            cancellationToken);
        if (search.ErrorMessage is not null)
        {
            return new GameArtworkSearchResponse { ErrorMessage = search.ErrorMessage };
        }

        var selectedGame = search.Value?.FirstOrDefault();
        if (selectedGame is null)
        {
            return new GameArtworkSearchResponse
            {
                ErrorMessage = "IGN did not find a game matching this title."
            };
        }

        var gallery = await client.GetArtworkDocumentAsync(
            selectedGame.Slug,
            preferredLanguageTag,
            cancellationToken);
        if (gallery.ErrorMessage is not null)
        {
            return new GameArtworkSearchResponse { ErrorMessage = gallery.ErrorMessage };
        }

        using var document = gallery.Value
            ?? throw new InvalidOperationException("IGN returned a successful artwork response without data.");
        var sources = IgnGraphQlParser.ReadArtwork(document.RootElement, selectedGame.PrimaryImageUrl);
        return new GameArtworkSearchResponse { Sources = sources };
    }
}
