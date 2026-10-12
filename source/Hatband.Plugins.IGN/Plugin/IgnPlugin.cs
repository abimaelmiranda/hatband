using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Plugins.IGN.Plugin;

public sealed class IgnPlugin : HatbandPlugin
{
    public override string Id => "ign";

    public override string Name => "IGN";

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("Hatband.Plugin.IGN", client => client.Timeout = TimeSpan.FromSeconds(20));
    }
}
