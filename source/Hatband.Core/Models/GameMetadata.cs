using Hatband.Core.Extensions;
using Hatband.Core.Enums;

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

        if (NativePlatforms is null)
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
        var releaseDate = ReleaseDate;
        if (!Overrides.ReleaseDate)
        {
            if (languageChanged)
            {
                releaseDate = downloaded.ReleaseDate ?? releaseDate;
            }
            else if (releaseDate is null)
            {
                releaseDate = downloaded.ReleaseDate;
            }
        }

        return this with
        {
            LanguageTag = requestedLanguageTag,
            StoreName = downloaded.StoreName.PreferNonWhiteSpace(StoreName),
            Description = MergeTextMetadataValue(
                Description,
                downloaded.Description,
                Overrides.Description,
                languageChanged),
            Developer = MergeTextMetadataValue(
                Developer,
                downloaded.Developer,
                Overrides.Developer,
                languageChanged),
            Publisher = MergeTextMetadataValue(
                Publisher,
                downloaded.Publisher,
                Overrides.Publisher,
                languageChanged),
            Genre = MergeTextMetadataValue(
                Genre,
                downloaded.Genre,
                Overrides.Genre,
                languageChanged),
            ReleaseDate = releaseDate,
            NativePlatforms = downloaded.NativePlatforms ?? NativePlatforms
        };
    }

    private static string? MergeTextMetadataValue(
        string? currentValue,
        string? downloadedValue,
        bool isOverridden,
        bool languageChanged)
    {
        if (isOverridden)
        {
            return currentValue;
        }

        if (languageChanged)
        {
            return downloadedValue.PreferNonWhiteSpace(currentValue);
        }

        return currentValue.PreferNonWhiteSpace(downloadedValue);
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

    public GameArtwork Artwork { get; init; } = new();

    public GameMetadataOverrides Overrides { get; init; } = new();
}
