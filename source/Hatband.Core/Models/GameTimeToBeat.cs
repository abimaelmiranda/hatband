namespace Hatband.Core.Models;

/// <summary>
/// HowLongToBeat data cached for a game. A record without an HLTB game ID records a completed search with no confident match.
/// </summary>
public sealed record GameTimeToBeat
{
    private static readonly TimeSpan MatchedSearchLifetime = TimeSpan.FromDays(180);
    private static readonly TimeSpan UnmatchedSearchLifetime = TimeSpan.FromDays(30);

    public int? HowLongToBeatGameId { get; init; }

    public string? HowLongToBeatName { get; init; }

    public long? MainStorySeconds { get; init; }

    public long? MainStoryPlusExtrasSeconds { get; init; }

    public long? CompletionistSeconds { get; init; }

    public DateTime? LastSearchedAtUtc { get; init; }

    public static GameTimeToBeat FromSearchResult(HowLongToBeatGame? match, DateTimeOffset searchedAtUtc)
    {
        if (match is null)
        {
            return new GameTimeToBeat
            {
                LastSearchedAtUtc = searchedAtUtc.UtcDateTime
            };
        }

        return new GameTimeToBeat
        {
            HowLongToBeatGameId = match.Id,
            HowLongToBeatName = match.Name,
            MainStorySeconds = match.MainStorySeconds,
            MainStoryPlusExtrasSeconds = match.MainStoryPlusExtrasSeconds,
            CompletionistSeconds = match.CompletionistSeconds,
            LastSearchedAtUtc = searchedAtUtc.UtcDateTime
        };
    }

    public bool IsSearchResultFresh(DateTime utcNow)
    {
        if (LastSearchedAtUtc is not DateTime lastSearchedAtUtc)
        {
            return false;
        }

        var lifetime = HowLongToBeatGameId is null ? UnmatchedSearchLifetime : MatchedSearchLifetime;
        return utcNow - lastSearchedAtUtc < lifetime;
    }
}
