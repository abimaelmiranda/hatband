using Hatband.Core.Enums.Artwork;

namespace Hatband.Core.Models.Games;

/// <summary>
/// Image content returned for a game's artwork picker.
/// </summary>
public sealed record GameArtworkImage
{
    public required GameArtworkSlot Slot { get; init; }

    public required byte[] Content { get; init; }

    public required string ContentType { get; init; }
}
