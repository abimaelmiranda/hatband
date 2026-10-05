using System.Text.Json;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Extensions;

namespace Hatband.Integrations.IGN;

internal static class IgnGraphQlParser
{
    public static IReadOnlyList<IgnGameSearchEntry> ReadSearchResults(JsonElement root, string region)
    {
        var search = GetProperty(GetProperty(root, "data"), "SearchObjectsByName");
        var objects = GetProperty(search, "objects");
        if (objects.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("IGN returned a response without search results.");
        }

        var results = new List<IgnGameSearchEntry>();
        foreach (var item in objects.EnumerateArray())
        {
            var id = GetString(item, "id");
            var slug = GetString(item, "slug");
            var name = GetString(GetProperty(GetProperty(item, "metadata"), "names"), "name")
                ?? GetString(item, "name");
            if (id is null || slug is null || name is null)
            {
                continue;
            }

            results.Add(new IgnGameSearchEntry
            {
                Id = id,
                Name = name,
                Slug = slug,
                ReleaseDate = GetReleaseDate(item, region),
                Platforms = GetPlatforms(item),
                PrimaryImageUrl = GetString(GetProperty(item, "primaryImage"), "url")
            });
        }

        return results;
    }

    public static IgnGameDetails? ReadDetails(JsonElement root, string region)
    {
        var details = GetProperty(GetProperty(root, "data"), "ObjectSelectByTypeAndSlug");
        if (details.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var releaseDateText = GetReleaseDate(details, region);
        DateOnly? releaseDate = releaseDateText.TryParseIsoDate(out var parsedDate) ? parsedDate : null;

        return new IgnGameDetails
        {
            Description = GetDescription(details),
            Developer = JoinAttributes(details, "producers"),
            Publisher = JoinAttributes(details, "publishers"),
            Genre = JoinAttributes(details, "genres"),
            ReleaseDate = releaseDate
        };
    }

    public static IReadOnlyList<(string Url, GameArtworkSlot Slot)> ReadArtwork(
        JsonElement root,
        string? primaryImageUrl)
    {
        var sources = new List<(string Url, GameArtworkSlot Slot)>();
        var knownUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (primaryImageUrl is not null)
        {
            if (knownUrls.Add(primaryImageUrl))
            {
                sources.Add((primaryImageUrl, GameArtworkSlot.Cover));
            }
        }

        var gallery = GetProperty(
            GetProperty(GetProperty(GetProperty(root, "data"), "ObjectImageGallery"), "imageGallery"),
            "images");
        if (gallery.ValueKind == JsonValueKind.Array)
        {
            foreach (var image in gallery.EnumerateArray())
            {
                var url = GetString(image, "url");
                if (url is null || !knownUrls.Add(url))
                {
                    continue;
                }

                sources.Add((url, GameArtworkSlot.Background));
            }
        }

        return sources;
    }

    public static string? ReadApiError(JsonElement response)
    {
        var errors = GetProperty(response, "errors");
        if (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
        {
            return null;
        }

        var firstError = errors[0];
        var message = GetString(firstError, "message");
        var code = GetString(GetProperty(firstError, "extensions"), "code");
        if (message is not null && code is not null)
        {
            return $"{message} ({code})";
        }

        return message ?? code;
    }

    public static string? ReadApiError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return ReadApiError(document.RootElement);
        }
        catch (JsonException)
        {
            var message = responseBody.Trim();
            return message.Length == 0 ? null : message[..Math.Min(message.Length, 240)];
        }
    }

    private static string? GetDescription(JsonElement details)
    {
        var descriptions = GetProperty(GetProperty(details, "metadata"), "descriptions");
        return GetString(descriptions, "long") ?? GetString(descriptions, "short");
    }

    private static string? JoinAttributes(JsonElement details, string propertyName)
    {
        var values = GetProperty(details, propertyName);
        if (values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = values.EnumerateArray()
            .Select(item => GetString(item, "name"))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? null : string.Join(", ", names);
    }

    private static IReadOnlyList<string> GetPlatforms(JsonElement game)
    {
        var regions = GetProperty(game, "objectRegions");
        if (regions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in regions.EnumerateArray())
        {
            var releases = GetProperty(region, "releases");
            if (releases.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var release in releases.EnumerateArray())
            {
                var attributes = GetProperty(release, "platformAttributes");
                if (attributes.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var attribute in attributes.EnumerateArray())
                {
                    var name = GetString(attribute, "name");
                    if (name is not null)
                    {
                        platforms.Add(name);
                    }
                }
            }
        }

        return platforms.ToArray();
    }

    private static string? GetReleaseDate(JsonElement game, string region)
    {
        var regions = GetProperty(game, "objectRegions");
        if (regions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var regionObjects = regions.EnumerateArray().ToArray();
        var selectedRegion = regionObjects.FirstOrDefault(candidate =>
            string.Equals(GetString(candidate, "region"), region, StringComparison.OrdinalIgnoreCase));
        if (selectedRegion.ValueKind != JsonValueKind.Object)
        {
            selectedRegion = regionObjects.FirstOrDefault();
        }

        var releases = GetProperty(selectedRegion, "releases");
        if (releases.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return releases.EnumerateArray()
            .Select(release => GetString(release, "date"))
            .OfType<string>()
            .OrderBy(date => date, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return default;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        var value = GetProperty(element, propertyName);
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
