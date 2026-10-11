using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.PostInstall;
using Hatband.Integrations.Steam.Protondb;
using Hatband.Integrations.Settings;
using Microsoft.Extensions.DependencyInjection;
using Hatband.SDK.Plugins;

namespace Hatband.Integrations.Steam;

public sealed class SteamPlugin : HatbandPlugin
{
    public override string Id => "steam";

    public override string Name => "Steam";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(SteamStoreIntegration),
        typeof(SteamArtworkProvider),
        typeof(SteamGameInstallationProvider),
        typeof(SteamGameManagementProvider),
        typeof(SteamGamePostInstallProvider),
        typeof(SteamMetadataProvider),
        typeof(ConnectorsSettingsSection),
    ];

    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISteamPlayerService, SteamPlayerService>();
        services.AddSingleton<ISteamTokenRefresher, SteamKitTokenRefresher>();
        services.AddSingleton<SteamKitConnectionFactory>();
        services.AddSingleton<ISteamInstallationService, SteamInstallationService>();
        services.AddSingleton<ISteamInstalledGameScanner, SteamInstalledGameScanner>();
        services.AddSingleton<SteamGameLaunchOptionsProvider>();
        services.AddSingleton<ProtondbMetadataProvider>();
    }
}
