using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Integrations.IGN;

/// <summary>
/// Searches IGN's primary artwork and image gallery for a game title.
/// </summary>
public sealed class IgnArtworkProvider : IGameArtworkProvider
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IgnGraphQlClient client;

    public IgnArtworkProvider(
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.httpClientFactory = httpClientFactory;
        client = new IgnGraphQlClient(httpClientFactory, cacheService, timeProvider);
    }

    public GameSourceId? SourceId => null;

    public string DisplayName => "IGN";

    public Task<IReadOnlyList<GameArtworkImage>> GetDefaultArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GameArtworkImage>>([]);
    }

    public async Task<IReadOnlyList<GameArtworkImage>> GetArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);

        var search = await client.SearchGamesAsync(game.Name, "US", languageTag, cancellationToken);
        if (search.ErrorMessage is not null)
        {
            return [];
        }

        var selectedGame = search.Value?.FirstOrDefault();
        if (selectedGame is null)
        {
            return [];
        }

        var gallery = await client.GetArtworkDocumentAsync(
            selectedGame.Slug,
            languageTag,
            cancellationToken);
        if (gallery.ErrorMessage is not null)
        {
            return [];
        }

        using var document = gallery.Value
            ?? throw new InvalidOperationException("IGN returned a successful artwork response without data.");
        var sources = IgnGraphQlParser.ReadArtwork(document.RootElement, selectedGame.PrimaryImageUrl);
        var images = new List<GameArtworkImage>(sources.Count);
        using var httpClient = httpClientFactory.CreateClient();
        foreach (var (url, slot) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var imageUri) ||
                (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            try
            {
                using var response = await httpClient.GetAsync(
                    imageUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (content.Length == 0)
                {
                    continue;
                }

                images.Add(new GameArtworkImage
                {
                    Slot = slot,
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
                });
            }
            catch (HttpRequestException)
            {
                // A broken gallery image should not prevent the remaining choices from loading.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A timed-out gallery image should not prevent the remaining choices from loading.
            }
        }

        return images;
    }
}
