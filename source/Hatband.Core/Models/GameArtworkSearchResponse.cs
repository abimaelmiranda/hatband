namespace Hatband.Core.Models;

/// <summary>
/// Artwork locations found by a manual artwork source.
/// </summary>
public sealed record GameArtworkSearchResponse
{
    public GameArtworkSources Sources { get; init; } = new();

    public string? ErrorMessage { get; init; }
}
