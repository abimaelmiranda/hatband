using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Integrations.HowLongToBeat;

public sealed class HowLongToBeatPlugin : HatbandPlugin
{
    public override string Id => "howlongtobeat";

    public override string Name => "HowLongToBeat";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(HowLongToBeatProvider),
    ];

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }
}
