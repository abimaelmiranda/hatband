using System.Text.Json.Serialization;

namespace Hatband.Plugins.Proton.Models;

[JsonSerializable(typeof(GitHubRelease[]))]
internal partial class GitHubReleaseJsonSerializerContext : JsonSerializerContext
{
}
