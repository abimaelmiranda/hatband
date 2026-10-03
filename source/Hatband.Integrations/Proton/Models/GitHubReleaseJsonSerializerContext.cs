using System.Text.Json.Serialization;

namespace Hatband.Integrations.Proton.Models;

[JsonSerializable(typeof(GitHubRelease[]))]
internal partial class GitHubReleaseJsonSerializerContext : JsonSerializerContext
{
}
