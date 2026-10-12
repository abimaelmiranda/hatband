using System.Reflection;

namespace Hatband.SDK.Plugins;

/// <summary>Host supplied assembly sharing and capability contracts for plugin loading.</summary>
public sealed class PluginLoadingOptions
{
    public PluginLoadingOptions(
        string builtInPluginDirectory,
        IEnumerable<Assembly>? sharedAssemblies = null,
        IEnumerable<Type>? additionalCapabilityTypes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builtInPluginDirectory);
        BuiltInPluginDirectory = builtInPluginDirectory;
        AdditionalCapabilityTypes = (additionalCapabilityTypes ?? []).Distinct().ToArray();
        if (AdditionalCapabilityTypes.Any(type => !type.IsInterface))
        {
            throw new ArgumentException("Additional plugin capabilities must be interface types.", nameof(additionalCapabilityTypes));
        }

        SharedAssemblies = (sharedAssemblies ?? [])
            .Concat(AdditionalCapabilityTypes.Select(type => type.Assembly))
            .Distinct()
            .ToArray();
    }

    public string BuiltInPluginDirectory { get; }

    public IReadOnlyList<Assembly> SharedAssemblies { get; }

    public IReadOnlyList<Type> AdditionalCapabilityTypes { get; }
}
