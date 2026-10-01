namespace Hatband.Core.Models;

/// <summary>
/// References to a game's local artwork files.
/// </summary>
public sealed record GameArtwork
{
    public string? CoverImagePath { get; init; }

    public string? BackgroundImagePath { get; init; }

    public string? IconPath { get; init; }

    public bool IsCoverCustomized { get; init; }

    public bool IsBackgroundCustomized { get; init; }

    public bool IsIconCustomized { get; init; }
}
