namespace Hatband.Integrations.IGN;

internal sealed record IgnGameSearchEntry
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public string? ReleaseDate { get; init; }

    public required IReadOnlyList<string> Platforms { get; init; }

    public string? PrimaryImageUrl { get; init; }
}
