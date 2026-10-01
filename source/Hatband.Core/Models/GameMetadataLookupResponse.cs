namespace Hatband.Core.Models;

/// <summary>
/// Metadata returned for a manually selected search result.
/// </summary>
public sealed record GameMetadataLookupResponse
{
    public GameMetadata? Metadata { get; init; }

    public string? RequestedLanguageTag { get; init; }

    public string? ContentLanguageTag { get; init; }

    public bool IsContentLanguageMatch { get; init; }

    public Uri? SourceUri { get; init; }

    public string? ErrorMessage { get; init; }
}
