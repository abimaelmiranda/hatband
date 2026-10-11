namespace Hatband.Infrastructure.Plugins;

public sealed class PluginCatalog
{
    public PluginCatalog(IReadOnlyList<LoadedPlugin> plugins, IReadOnlyList<PluginLoadIssue> issues)
    {
        Plugins = plugins;
        Issues = issues;
    }

    public IReadOnlyList<LoadedPlugin> Plugins { get; }

    public IReadOnlyList<PluginLoadIssue> Issues { get; }
}
