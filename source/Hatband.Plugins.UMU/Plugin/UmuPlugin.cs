using Hatband.SDK.Plugins;
using Hatband.Plugins.UMU.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Plugins.UMU.Plugin;

public sealed class UmuPlugin : HatbandPlugin
{
    public override string Id => "umu";

    public override string Name => "UMU";

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("Hatband.Plugin.UMU", client => client.Timeout = TimeSpan.FromMinutes(2));
        services.AddSingleton<UmuDependencyService>();
    }

}
