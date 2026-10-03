using System.Text.Json.Serialization;

namespace Hatband.Integrations.Proton.Models;

internal sealed record GitHubReleaseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
