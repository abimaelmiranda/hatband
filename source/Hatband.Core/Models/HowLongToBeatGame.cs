namespace Hatband.Core.Models;

/// <summary>
/// A candidate returned by a HowLongToBeat game search.
/// </summary>
public sealed record HowLongToBeatGame
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public int? ReleaseYear { get; init; }

    public long? MainStorySeconds { get; init; }

    public long? MainStoryPlusExtrasSeconds { get; init; }

    public long? CompletionistSeconds { get; init; }

    public int? MainStorySubmissionCount { get; init; }
}
