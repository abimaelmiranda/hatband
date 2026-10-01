namespace Hatband.Core.Models;

/// <summary>
/// A remote image offered by a metadata source for manual selection.
/// </summary>
public sealed record GameArtworkCandidate
{
    public required string Url { get; init; }

    public string? Caption { get; init; }

    public bool IsPrimaryImage { get; init; }
}
