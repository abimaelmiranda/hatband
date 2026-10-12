using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Plugins.HowLongToBeat.Plugin;

public sealed class HowLongToBeatPlugin : HatbandPlugin
{
    public override string Id => "howlongtobeat";

    public override string Name => "HowLongToBeat";

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("Hatband.Plugin.HowLongToBeat", client => client.Timeout = TimeSpan.FromSeconds(20));
    }
}
