namespace Hatband.Core.Models.Games;

/// <summary>
/// HowLongToBeat data cached for a game. A non-null record without an HLTB game ID means a search was attempted with no result.
/// </summary>
public sealed record GameTimeToBeat
{
    public int? HowLongToBeatGameId { get; init; }

    public string? HowLongToBeatName { get; init; }

    public long? MainStorySeconds { get; init; }

    public long? MainStoryPlusExtrasSeconds { get; init; }

    public long? CompletionistSeconds { get; init; }

    public static GameTimeToBeat FromSearchResult(HowLongToBeatGame? match)
    {
        if (match is null)
        {
            return new GameTimeToBeat();
        }

        return new GameTimeToBeat
        {
            HowLongToBeatGameId = match.Id,
            HowLongToBeatName = match.Name,
            MainStorySeconds = match.MainStorySeconds,
            MainStoryPlusExtrasSeconds = match.MainStoryPlusExtrasSeconds,
            CompletionistSeconds = match.CompletionistSeconds
        };
    }
}
