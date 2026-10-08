using Hatband.Core.Abstractions.Services;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.Archives;
using Hatband.Infrastructure.Authentication;
using Hatband.Infrastructure.FileSystem;
using Hatband.Infrastructure.Host;
using Hatband.Infrastructure.Persistence;
using Hatband.Infrastructure.Persistence.Repositories;
using Hatband.Infrastructure.Services.CompatibilityTools;
using Hatband.Infrastructure.Services.Caching;
using Hatband.Infrastructure.Services.Games;
using Hatband.Infrastructure.Settings;
using Hatband.Infrastructure.Storage.Artwork;
using Hatband.Integrations;
using Hatband.Integrations.HowLongToBeat;
using Hatband.Integrations.Steam;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Protondb;
using Hatband.Integrations.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hatband.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddHatbandInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);

        services.AddLogging();
        services.AddDbContextFactory<HatbandDbContext>(options => options.UseSqlite(connectionString));
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICacheService, CacheService>();
        services.AddHttpClient(string.Empty, client => client.Timeout = TimeSpan.FromSeconds(25));
        services.AddHttpClient("Artwork", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("SteamPlayer", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("Protondb", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton(appDataFileSystem);
        services.TryAddSingleton<IHostSystemInfo, HostSystemInfo>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IGameRepository, GameRepository>();
        services.AddSingleton<IGameLibraryRepository, GameLibraryRepository>();
        services.AddSingleton<IGameArtworkStorage>(provider => new FileSystemGameArtworkStorage(appDataFileSystem));
        services.AddSingleton<ISettingsSection, GeneralSettingsSection>();
        services.AddSingleton<ISettingsSection, ConnectorsSettingsSection>();
        services.AddSingleton<ISettingsApi, JsonSettingsApi>();
        services.AddSingleton<ISecureSecretVault, OperatingSystemSecretVault>();
        services.AddSingleton<IConnectorSessionStore, EncryptedConnectorSessionStore>();
        services.AddSingleton<IArchiveExtractionService, SharpCompressArchiveExtractionService>();
        services.AddSingleton<ICompatibilityToolReleaseCatalogService, CompatibilityToolReleaseCatalogService>();
        services.AddSingleton<ICompatibilityToolDiscoveryService, CompatibilityToolDiscoveryService>();
        services.AddSingleton<ICompatibilityToolInstallationService, CompatibilityToolInstallationService>();
        services.AddSingleton<UmuDependencyService>();
        services.AddSingleton<ProtonExecutionService>();
        services.AddSingleton<IGameManagementService, GameManagementService>();
        services.AddKeyedSingleton<IGameManagementService, ManualGameManagementProvider>(GameSourceId.Manual);
        services.AddKeyedSingleton<IGameManagementService, SteamGameManagementProvider>(GameSourceId.Steam);
        services.AddSingleton<IGameProcessMonitor, GameProcessMonitor>();
        services.AddSingleton<IGameInstallationStateSyncService, GameInstallationStateSyncService>();
        services.AddSingleton<IHostApplicationLauncher, HostApplicationLauncher>();
        services.AddSingleton<IGameLibrarySyncService, GameLibrarySyncService>();
        services.AddSingleton<IHowLongToBeatProvider, HowLongToBeatProvider>();
        services.AddSingleton<ProtondbMetadataProvider>();
        services.AddSingleton<ISteamPlayerService, SteamPlayerService>();
        services.AddSingleton<ISteamTokenRefresher, SteamKitTokenRefresher>();
        services.AddSingleton<ISteamInstallationService, SteamInstallationService>();
        services.AddSingleton<ISteamInstalledGameScanner, SteamInstalledGameScanner>();

        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<ICompatibilityToolReleaseProvider>())
            .As<ICompatibilityToolReleaseProvider>()
            .WithSingletonLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<IGameStoreIntegration>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<IGameMetadataProvider>())
            .As<IGameMetadataProvider>()
            .WithSingletonLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<IGameArtworkProvider>())
            .As<IGameArtworkProvider>()
            .WithSingletonLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<IGameInstallationProvider>())
            .As<IGameInstallationProvider>()
            .WithSingletonLifetime());

        return services;
    }
}
