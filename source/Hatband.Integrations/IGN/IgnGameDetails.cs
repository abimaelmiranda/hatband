namespace Hatband.Integrations.IGN;

internal sealed record IgnGameDetails
{
    public string? Description { get; init; }

    public string? Developer { get; init; }

    public string? Publisher { get; init; }

    public string? Genre { get; init; }

    public DateOnly? ReleaseDate { get; init; }
}
