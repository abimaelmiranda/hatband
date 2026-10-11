namespace Hatband.SDK.Plugins;

/// <summary>
/// Describes an external plugin package. Store as <c>hatband-plugin.json</c>.
/// </summary>
public sealed class PluginManifest
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required int ApiVersion { get; init; }

    public required string EntryAssembly { get; init; }

    public required string EntryType { get; init; }
}
