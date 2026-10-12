using Hatband.Plugins.Steam.Abstractions;
using Hatband.Plugins.Steam.Authentication;
using Hatband.Plugins.Steam.Games;
using Hatband.Plugins.Steam.Installations;
using Hatband.Plugins.Steam.PostInstall;
using Hatband.Plugins.Steam.Protondb;
using Microsoft.Extensions.DependencyInjection;
using Hatband.SDK.Plugins;

namespace Hatband.Plugins.Steam.Plugin;

public sealed class SteamPlugin : HatbandPlugin
{
    public override string Id => "steam";

    public override string Name => "Steam";

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient("Hatband.Plugin.Steam", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("Hatband.Plugin.Steam.Player", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("Hatband.Plugin.Steam.Protondb", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<ISteamPlayerService, SteamPlayerService>();
        services.AddSingleton<ISteamTokenRefresher, SteamKitTokenRefresher>();
        services.AddSingleton<SteamKitConnectionFactory>();
        services.AddSingleton<ISteamInstallationService, SteamInstallationService>();
        services.AddSingleton<ISteamInstalledGameScanner, SteamInstalledGameScanner>();
        services.AddSingleton<SteamGameLaunchOptionsProvider>();
        services.AddSingleton<ProtondbMetadataProvider>();
    }
}
