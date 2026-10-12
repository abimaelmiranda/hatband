using System.Text.Json;
using Hatband.Core.Enums.Artwork;

namespace Hatband.Plugins.SteamGridDB.Api;

internal static class SteamGridDBAssetParser
{
    public static IReadOnlyList<SteamGridDBAsset> ReadAssets(JsonElement root, GameArtworkSlot slot)
    {
        SteamGridDBGameParser.EnsureSuccessfulResponse(root);
        if (!root.TryGetProperty("data", out var data))
        {
            throw new InvalidDataException("SteamGridDB returned a malformed artwork response.");
        }

        var assets = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("assets", out var nested)
            ? nested
            : data;
        if (assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("SteamGridDB returned a malformed artwork list.");
        }

        var results = new List<SteamGridDBAsset>();
        foreach (var item in assets.EnumerateArray())
        {
            if (!item.TryGetProperty("url", out var urlValue) || urlValue.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(urlValue.GetString(), UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            {
                continue;
            }

            var contentType = ReadContentType(item, url);
            if (contentType is null)
            {
                continue;
            }

            var width = ReadDimension(item, "width");
            var height = ReadDimension(item, "height");
            var isAnimated = IsTrue(item, "animated") || IsTrue(item, "is_animated");
            var isUnsafe = IsTrue(item, "nsfw") || IsTrue(item, "epilepsy") || IsTrue(item, "humor") ||
                           IsTrue(item, "is_deleted") || IsTrue(item, "processing");
            var meetsDimensions = slot switch
            {
                GameArtworkSlot.Cover => width == 600 && height == 900,
                GameArtworkSlot.Background => width is not null && height is not null && width > height,
                GameArtworkSlot.Icon or GameArtworkSlot.Logo =>
                    (width is null || width > 0) && (height is null || height > 0),
                _ => false
            };
            if (!meetsDimensions || isAnimated || isUnsafe || !IsAcceptedType(slot, contentType))
            {
                continue;
            }

            results.Add(new SteamGridDBAsset(url, slot, NormalizeContentType(contentType)));
        }

        return results;
    }

    public static bool IsAcceptedType(GameArtworkSlot slot, string contentType)
    {
        var normalized = NormalizeContentType(contentType);
        if (slot is GameArtworkSlot.Icon or GameArtworkSlot.Logo)
        {
            return normalized == "image/png";
        }

        return normalized is "image/png" or "image/jpeg";
    }

    private static string? ReadContentType(JsonElement item, Uri url)
    {
        if (item.TryGetProperty("mime", out var mime) && mime.ValueKind == JsonValueKind.String && mime.GetString() is { } mimeType)
        {
            return mimeType;
        }

        if (item.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String && format.GetString() is { } formatType)
        {
            return formatType;
        }

        return Path.GetExtension(url.AbsolutePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };
    }

    private static int? ReadDimension(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var dimension))
        {
            return dimension;
        }

        return null;
    }

    private static bool IsTrue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string NormalizeContentType(string value) => value.ToLowerInvariant() switch
    {
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        var contentType => contentType
    };
}
