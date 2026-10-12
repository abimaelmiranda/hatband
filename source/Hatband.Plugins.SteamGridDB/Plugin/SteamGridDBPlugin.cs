using Hatband.Plugins.SteamGridDB.Settings;
using Hatband.Plugins.SteamGridDB.Authentication;
using Hatband.Plugins.SteamGridDB.Api;
using Hatband.Plugins.SteamGridDB.Localization;
using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Plugins.SteamGridDB.Plugin;

public sealed class SteamGridDBPlugin : HatbandPlugin
{
    public override string Id => "steamgriddb";

    public override string Name => SteamGridDBResources.PluginName;

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SteamGridDBRequestLimitHandler>();
        services.AddHttpClient("Hatband.Plugin.SteamGridDB.Scraper", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Hatband/1.0; artwork lookup)");
        }).AddHttpMessageHandler<SteamGridDBRequestLimitHandler>();
        services.AddHttpClient("Hatband.Plugin.SteamGridDB.Api", client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<SteamGridDBRequestLimitHandler>();
        services.AddSingleton<SteamGridDBCredentialService>();
        services.AddSingleton<SteamGridDBScraperClient>();
        services.AddSingleton<SteamGridDBApiClient>();
    }
}
