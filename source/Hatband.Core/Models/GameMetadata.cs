using Hatband.Core.Extensions;

namespace Hatband.Core.Models;

/// <summary>
/// Descriptive information about a game, without its library identity or user state.
/// </summary>
public sealed record GameMetadata
{
    public bool NeedsStoreRefresh(string gameName, bool isNameCustomized, string requestedLanguageTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguageTag);

        var languageChanged = !string.Equals(LanguageTag, requestedLanguageTag, StringComparison.OrdinalIgnoreCase);
        if (!isNameCustomized &&
            (languageChanged ||
             (!string.IsNullOrWhiteSpace(StoreName) &&
              !string.Equals(gameName, StoreName, StringComparison.Ordinal))))
        {
            return true;
        }

        if (!Overrides.Description && (languageChanged || string.IsNullOrWhiteSpace(Description)))
        {
            return true;
        }

        if (!Overrides.Developer && (languageChanged || string.IsNullOrWhiteSpace(Developer)))
        {
            return true;
        }

        if (!Overrides.Publisher && (languageChanged || string.IsNullOrWhiteSpace(Publisher)))
        {
            return true;
        }

        if (!Overrides.Genre && (languageChanged || string.IsNullOrWhiteSpace(Genre)))
        {
            return true;
        }

        return !Overrides.ReleaseDate && (languageChanged || ReleaseDate is null);
    }

    public GameMetadata MergeDownloaded(GameMetadata downloaded, string requestedLanguageTag)
    {
        ArgumentNullException.ThrowIfNull(downloaded);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedLanguageTag);

        var languageChanged = !string.Equals(LanguageTag, requestedLanguageTag, StringComparison.OrdinalIgnoreCase);
        return this with
        {
            LanguageTag = requestedLanguageTag,
            StoreName = downloaded.StoreName.PreferNonWhiteSpace(StoreName),
            Description = Overrides.Description
                ? Description
                : languageChanged
                    ? downloaded.Description.PreferNonWhiteSpace(Description)
                    : Description.PreferNonWhiteSpace(downloaded.Description),
            Developer = Overrides.Developer
                ? Developer
                : languageChanged
                    ? downloaded.Developer.PreferNonWhiteSpace(Developer)
                    : Developer.PreferNonWhiteSpace(downloaded.Developer),
            Publisher = Overrides.Publisher
                ? Publisher
                : languageChanged
                    ? downloaded.Publisher.PreferNonWhiteSpace(Publisher)
                    : Publisher.PreferNonWhiteSpace(downloaded.Publisher),
            Genre = Overrides.Genre
                ? Genre
                : languageChanged
                    ? downloaded.Genre.PreferNonWhiteSpace(Genre)
                    : Genre.PreferNonWhiteSpace(downloaded.Genre),
            ReleaseDate = Overrides.ReleaseDate
                ? ReleaseDate
                : languageChanged
                    ? downloaded.ReleaseDate ?? ReleaseDate
                    : ReleaseDate ?? downloaded.ReleaseDate
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

    public GameArtwork Artwork { get; init; } = new();

    public GameMetadataOverrides Overrides { get; init; } = new();
}
