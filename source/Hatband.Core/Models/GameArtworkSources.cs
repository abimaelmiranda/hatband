namespace Hatband.Core.Models;

/// <summary>
/// Candidate remote locations for a game's artwork.
/// </summary>
public sealed record GameArtworkSources
{
    public IReadOnlyList<string> CoverImageUrls { get; init; } = [];

    public IReadOnlyList<string> BackgroundImageUrls { get; init; } = [];

    public IReadOnlyList<GameArtworkCandidate> CoverImageCandidates { get; init; } = [];

    public IReadOnlyList<GameArtworkCandidate> BackgroundImageCandidates { get; init; } = [];

    public IReadOnlyList<string> IconUrls { get; init; } = [];
}
