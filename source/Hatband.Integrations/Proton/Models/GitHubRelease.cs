using System.Text.Json.Serialization;

namespace Hatband.Integrations.Proton.Models;

internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAsset> Assets);
