using Hatband.Core.Enums.Artwork;

namespace Hatband.Core.Models.Games;

/// <summary>
/// References to a game's local artwork files.
/// </summary>
public sealed record GameArtwork
{
    public string? CoverImagePath { get; init; }

    public string? BackgroundImagePath { get; init; }

    public string? IconPath { get; init; }

    public string? LogoPath { get; init; }

    public bool IsEmpty => CoverImagePath is null && BackgroundImagePath is null && IconPath is null && LogoPath is null;

    public string? GetPath(GameArtworkSlot slot)
    {
        return slot switch
        {
            GameArtworkSlot.Cover => CoverImagePath,
            GameArtworkSlot.Background => BackgroundImagePath,
            GameArtworkSlot.Icon => IconPath,
            GameArtworkSlot.Logo => LogoPath,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown artwork slot.")
        };
    }

    public GameArtwork WithPath(GameArtworkSlot slot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return slot switch
        {
            GameArtworkSlot.Cover => this with { CoverImagePath = path },
            GameArtworkSlot.Background => this with { BackgroundImagePath = path },
            GameArtworkSlot.Icon => this with { IconPath = path },
            GameArtworkSlot.Logo => this with { LogoPath = path },
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown artwork slot.")
        };
    }
}
