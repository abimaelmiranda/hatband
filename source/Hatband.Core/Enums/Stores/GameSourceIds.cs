using System.Text.RegularExpressions;

namespace Hatband.Core.Enums.Stores;

/// <summary>
/// Stable identifiers for built-in game sources. External plugins can register their own IDs.
/// </summary>
public static partial class GameSourceIds
{
    // Preserve the stored identifiers used by existing game rows and connector credentials.
    public const string Manual = "manual";

    public const string Steam = "steam";

    public static string Validate(string sourceId, string parameterName = "sourceId")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId, parameterName);
        if (!SourceIdPattern().IsMatch(sourceId))
        {
            throw new ArgumentException(
                "Source IDs must be lowercase and contain only letters, digits, dots, hyphens, or underscores.",
                parameterName);
        }

        return sourceId;
    }

    [GeneratedRegex(@"\A[a-z0-9._-]{1,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SourceIdPattern();
}
