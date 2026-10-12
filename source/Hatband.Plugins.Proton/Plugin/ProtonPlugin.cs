using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Plugins.Proton.Plugin;

public sealed class ProtonPlugin : HatbandPlugin
{
    public override string Id => "proton";

    public override string Name => "Proton releases";

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("Hatband.Plugin.Proton", client => client.Timeout = TimeSpan.FromSeconds(30));
    }
}
