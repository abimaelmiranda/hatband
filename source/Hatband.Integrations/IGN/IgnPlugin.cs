using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Integrations.IGN;

public sealed class IgnPlugin : HatbandPlugin
{
    public override string Id => "ign";

    public override string Name => "IGN";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(IgnArtworkProvider),
        typeof(IgnMetadataProvider),
    ];

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }
}
