using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Integrations.Proton;

public sealed class ProtonPlugin : HatbandPlugin
{
    public override string Id => "proton";

    public override string Name => "Proton releases";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(ProtonCachyOsReleaseProvider),
        typeof(ProtonGeReleaseProvider),
    ];

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }
}
