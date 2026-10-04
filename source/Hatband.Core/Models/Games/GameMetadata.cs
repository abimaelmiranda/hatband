using Hatband.Core.Extensions;
using Hatband.Core.Enums.Games;

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

        return this with
        {
            LanguageTag = LanguageTag ?? requestedLanguageTag,
            StoreName = StoreName.PreferNonWhiteSpace(downloaded.StoreName),
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

        return this with
        {
            LanguageTag = requestedLanguageTag,
            StoreName = downloaded.StoreName.PreferNonWhiteSpace(StoreName),
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

    public string? Description { get; init; }

    public string? Developer { get; init; }

    public string? Publisher { get; init; }

    public string? Genre { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Platforms the metadata source declares as native targets. Null means the source did not provide this information.
    /// </summary>
    public GamePlatform? NativePlatforms { get; init; }

}
