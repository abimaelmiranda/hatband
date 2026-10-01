using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Searches a metadata source for a game selected manually by the user.
/// </summary>
public interface IGameMetadataSearchProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    bool CanSearch(GameSourceLookupRequest request);

    Task<GameMetadataSearchResponse> SearchAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default);

    Task<GameMetadataLookupResponse> GetDetailsAsync(
        GameMetadataSearchResult selection,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default);
}
