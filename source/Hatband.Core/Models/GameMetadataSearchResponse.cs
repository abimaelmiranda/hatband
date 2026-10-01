namespace Hatband.Core.Models;

/// <summary>
/// Search candidates or an error returned by a manual metadata source.
/// </summary>
public sealed record GameMetadataSearchResponse
{
    public IReadOnlyList<GameMetadataSearchResult> Results { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
