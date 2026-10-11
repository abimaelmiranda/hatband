namespace Hatband.SDK.Plugins;

public sealed record PluginGameDataValue<T>(int SchemaVersion, T Value);
