namespace Hatband.Core.Models;

/// <summary>
/// A game candidate returned by a manual metadata search.
/// </summary>
public sealed record GameMetadataSearchResult
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Opaque source-specific value used to load this candidate's details.
    /// </summary>
    public required string ProviderToken { get; init; }

    public string? ReleaseDate { get; init; }

    public IReadOnlyList<string> Platforms { get; init; } = [];

    public string? PrimaryImageUrl { get; init; }
}
