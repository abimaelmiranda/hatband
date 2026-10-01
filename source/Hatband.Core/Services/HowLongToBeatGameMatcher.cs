using Hatband.Core.Extensions;
using Hatband.Core.Models;

namespace Hatband.Core.Services;

public static class HowLongToBeatGameMatcher
{
    public static HowLongToBeatGame? FindConfidentMatch(
        Game game,
        IReadOnlyList<HowLongToBeatGame> candidates)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(candidates);

        var normalizedGameName = game.Name.NormalizeForLooseComparison();
        var exactMatches = candidates
            .Where(candidate => candidate.Name.NormalizeForLooseComparison() == normalizedGameName)
            .ToArray();

        if (exactMatches.Length == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Length == 0 || game.Metadata.ReleaseDate is not DateOnly releaseDate)
        {
            return null;
        }

        var releaseYearMatches = exactMatches
            .Where(candidate => candidate.ReleaseYear == releaseDate.Year)
            .ToArray();
        return releaseYearMatches.Length == 1 ? releaseYearMatches[0] : null;
    }
}
