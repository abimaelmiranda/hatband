namespace Hatband.Core.Models;

/// <summary>
/// Records metadata fields the user chose to keep, including intentionally empty values.
/// </summary>
public sealed record GameMetadataOverrides
{
    public bool Description { get; init; }

    public bool Developer { get; init; }

    public bool Publisher { get; init; }

    public bool Genre { get; init; }

    public bool ReleaseDate { get; init; }
}
