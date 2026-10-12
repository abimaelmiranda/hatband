using System.Text.Json;
using System.Text;

namespace Hatband.Plugins.SteamGridDB.Api;

internal static class SteamGridDBGameParser
{
    public static int ReadGameId(JsonElement root)
    {
        EnsureSuccessfulResponse(root);
        var data = ReadRequiredData(root);
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("SteamGridDB returned a malformed game response.");
        }

        var id = ReadId(data);
        if (id is null || id <= 0)
        {
            throw new InvalidDataException("SteamGridDB returned a game without a valid ID.");
        }

        return id.Value;
    }

    public static int? FindUniqueExactGameId(JsonElement root, string title)
    {
        EnsureSuccessfulResponse(root);
        var data = ReadRequiredData(root);
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("SteamGridDB returned a malformed game search response.");
        }

        var normalizedTitle = NormalizeTitle(title);
        var games = data.EnumerateArray().Select(ReadSearchResult).ToArray();
        var exactIds = games
            .Where(game => NormalizeTitle(game.Name) == normalizedTitle)
            .Select(game => game.Id)
            .Distinct()
            .Take(2)
            .ToArray();
        return exactIds.Length == 1 ? exactIds[0] : null;
    }

    private static (string Name, int Id) ReadSearchResult(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() is not { } gameName)
        {
            throw new InvalidDataException("SteamGridDB returned a malformed game search result.");
        }

        var id = ReadId(element);
        if (id is null || id <= 0)
        {
            throw new InvalidDataException("SteamGridDB returned a game search result without a valid ID.");
        }

        return (gameName, id.Value);
    }

    private static int? ReadId(JsonElement element)
    {
        if (element.TryGetProperty("id", out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericId))
        {
            return numericId;
        }

        return null;
    }

    private static JsonElement ReadRequiredData(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data))
        {
            throw new InvalidDataException("SteamGridDB response did not contain game data.");
        }

        return data;
    }

    public static void EnsureSuccessfulResponse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var success) ||
            success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("SteamGridDB returned a malformed response envelope.");
        }

        if (success.ValueKind == JsonValueKind.False)
        {
            throw new HttpRequestException("SteamGridDB returned an unsuccessful public response.");
        }
    }

    public static string NormalizeTitle(string title)
    {
        var characters = title.Replace("™", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("©", string.Empty, StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(characters);
    }
}
