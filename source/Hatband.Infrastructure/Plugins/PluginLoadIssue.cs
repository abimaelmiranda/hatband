namespace Hatband.Infrastructure.Plugins;

public sealed record PluginLoadIssue(string PluginDirectory, string Message, Exception? Exception);
