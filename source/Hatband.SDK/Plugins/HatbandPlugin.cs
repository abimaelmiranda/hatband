using Microsoft.Extensions.DependencyInjection;

namespace Hatband.SDK.Plugins;

/// <summary>
/// Entry point for a built-in or external Hatband extension.
/// </summary>
public abstract class HatbandPlugin
{
    /// <summary>
    /// Stable lowercase identifier declared by the plugin manifest.
    /// </summary>
    public abstract string Id { get; }

    public virtual string Name => Id;

    public virtual string Version => "1.0.0";

    /// <summary>
    /// Optional concrete provider types for built-in plugins that share an assembly.
    /// External plugins discover capabilities from their entry assembly when this is empty.
    /// </summary>
    public virtual IReadOnlyCollection<Type> CapabilityImplementations => [];

    /// <summary>
    /// Adds this plugin's services and capabilities to the host.
    /// </summary>
    public virtual void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }
}
