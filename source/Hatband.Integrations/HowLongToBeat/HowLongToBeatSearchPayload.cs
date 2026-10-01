using System.Text;
using System.Text.Json;
using Hatband.Integrations.HowLongToBeat.Models;

namespace Hatband.Integrations.HowLongToBeat;

internal static class HowLongToBeatSearchPayload
{
    public static HowLongToBeatSearchRequest Create(
        string gameName,
        HowLongToBeatSearchSessionToken sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        ArgumentNullException.ThrowIfNull(sessionToken);

        Dictionary<string, JsonElement>? honeypot = null;
        if (sessionToken.HoneypotKey is not null && sessionToken.HoneypotValue is not null)
        {
            honeypot = new Dictionary<string, JsonElement>
            {
                [sessionToken.HoneypotKey] = JsonSerializer.SerializeToElement(sessionToken.HoneypotValue)
            };
        }

        return new HowLongToBeatSearchRequest
        {
            SearchTerms = CreateSearchTerms(gameName),
            Options = new HowLongToBeatSearchOptions
            {
                Games = new HowLongToBeatGameOptions()
            },
            Honeypot = honeypot
        };
    }

    private static string[] CreateSearchTerms(string gameName)
    {
        var searchTerms = new List<string>();
        var currentTerm = new StringBuilder(gameName.Length);

        foreach (var character in gameName)
        {
            if (char.IsLetterOrDigit(character))
            {
                currentTerm.Append(character);
                continue;
            }

            if (character is '\'' or '\u2019')
            {
                continue;
            }

            AddCurrentTerm();
        }

        AddCurrentTerm();
        return searchTerms.ToArray();

        void AddCurrentTerm()
        {
            if (currentTerm.Length == 0)
            {
                return;
            }

            searchTerms.Add(currentTerm.ToString());
            currentTerm.Clear();
        }
    }
}
