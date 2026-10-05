using Hatband.Core.Extensions;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Stores;

namespace Hatband.Core.Models.Games;

/// <summary>
/// Descriptive information about a game, without its library identity or user state.
/// </summary>
public sealed record GameMetadata
{
    public GameMetadata MergeDownloaded(GameMetadata downloaded, string requestedLanguageTag)
    {
        ArgumentNullException.ThrowIfNull(downloaded);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguageTag);
        var (storeSourceId, storeGameId) = MergeStoreReference(downloaded, preferDownloaded: false);

        return this with
        {
            LanguageTag = LanguageTag ?? requestedLanguageTag,
            StoreName = StoreName.PreferNonWhiteSpace(downloaded.StoreName),
            StoreSourceId = storeSourceId,
            StoreGameId = storeGameId,
            Description = Description.PreferNonWhiteSpace(downloaded.Description),
            Developer = Developer.PreferNonWhiteSpace(downloaded.Developer),
            Publisher = Publisher.PreferNonWhiteSpace(downloaded.Publisher),
            Genre = Genre.PreferNonWhiteSpace(downloaded.Genre),
            ReleaseDate = ReleaseDate ?? downloaded.ReleaseDate,
            NativePlatforms = NativePlatforms ?? downloaded.NativePlatforms
        };
    }

    public GameMetadata RefreshFromDownloaded(GameMetadata downloaded, string requestedLanguageTag)
    {
        ArgumentNullException.ThrowIfNull(downloaded);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguageTag);
        var (storeSourceId, storeGameId) = MergeStoreReference(downloaded, preferDownloaded: true);

        return this with
        {
            LanguageTag = requestedLanguageTag,
            StoreName = downloaded.StoreName.PreferNonWhiteSpace(StoreName),
            StoreSourceId = storeSourceId,
            StoreGameId = storeGameId,
            Description = downloaded.Description.PreferNonWhiteSpace(Description),
            Developer = downloaded.Developer.PreferNonWhiteSpace(Developer),
            Publisher = downloaded.Publisher.PreferNonWhiteSpace(Publisher),
            Genre = downloaded.Genre.PreferNonWhiteSpace(Genre),
            ReleaseDate = downloaded.ReleaseDate ?? ReleaseDate,
            NativePlatforms = downloaded.NativePlatforms ?? NativePlatforms
        };
    }

    public string? LanguageTag { get; init; }

    /// <summary>
    /// The source's title in <see cref="LanguageTag"/>. A manual game name remains separate on <see cref="Game"/>.
    /// </summary>
    public string? StoreName { get; init; }

    /// <summary>
    /// Source that owns <see cref="StoreGameId"/>.
    /// </summary>
    public GameSourceId? StoreSourceId { get; init; }

    /// <summary>
    /// Identifier for this game in the metadata provider's store catalog, independent of its library source.
    /// </summary>
    public string? StoreGameId { get; init; }

    public string? Description { get; init; }

    public string? Developer { get; init; }

    public string? Publisher { get; init; }

    public string? Genre { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Platforms the metadata source declares as native targets. Null means the source did not provide this information.
    /// </summary>
    public GamePlatform? NativePlatforms { get; init; }

    private (GameSourceId? StoreSourceId, string? StoreGameId) MergeStoreReference(
        GameMetadata downloaded,
        bool preferDownloaded)
    {
        var hasExistingReference = StoreSourceId is not null && !string.IsNullOrWhiteSpace(StoreGameId);
        var hasDownloadedReference = downloaded.StoreSourceId is not null &&
                                     !string.IsNullOrWhiteSpace(downloaded.StoreGameId);

        if (preferDownloaded && hasDownloadedReference)
        {
            return (downloaded.StoreSourceId, downloaded.StoreGameId);
        }

        if (hasExistingReference)
        {
            return (StoreSourceId, StoreGameId);
        }

        if (hasDownloadedReference)
        {
            return (downloaded.StoreSourceId, downloaded.StoreGameId);
        }

        return (StoreSourceId, StoreGameId);
    }

}
