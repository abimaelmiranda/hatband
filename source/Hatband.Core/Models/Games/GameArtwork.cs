namespace Hatband.Core.Models.Games;

/// <summary>
/// References to a game's local artwork files.
/// </summary>
public sealed record GameArtwork
{
    public string? CoverImagePath { get; init; }

    public string? BackgroundImagePath { get; init; }

    public string? IconPath { get; init; }
}
