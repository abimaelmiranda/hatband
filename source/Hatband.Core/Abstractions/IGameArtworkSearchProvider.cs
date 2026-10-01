using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Searches for artwork independently of automatic store artwork imports.
/// </summary>
public interface IGameArtworkSearchProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    bool CanSearch(GameSourceLookupRequest request);

    Task<GameArtworkSearchResponse> SearchArtworkAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default);
}
