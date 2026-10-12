using Hatband.Core.Abstractions.Services;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.Archives;
using Hatband.Infrastructure.Authentication;
using Hatband.Infrastructure.FileSystem;
using Hatband.Infrastructure.Host;
using Hatband.Infrastructure.Persistence;
using Hatband.Infrastructure.Persistence.Repositories;
using Hatband.Infrastructure.Plugins;
using Hatband.Infrastructure.Services.CompatibilityTools;
using Hatband.Infrastructure.Services.Caching;
using Hatband.Infrastructure.Services.Games;
using Hatband.Infrastructure.Services.Games.PostInstall;
using Hatband.Infrastructure.Settings;
using Hatband.Infrastructure.Storage.Artwork;
using Hatband.SDK.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hatband.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddHatbandInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IAppDataFileSystem appDataFileSystem,
        PluginLoadingOptions? pluginLoadingOptions = null)
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
        services.AddSingleton(appDataFileSystem);
        services.TryAddSingleton<IHostSystemInfo, HostSystemInfo>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IGameRepository, GameRepository>();
        services.AddSingleton<IGameLibraryRepository, GameLibraryRepository>();
        services.AddSingleton<IGameArtworkStorage>(provider => new FileSystemGameArtworkStorage(appDataFileSystem));
        services.AddSingleton<ISettingsSection, GeneralSettingsSection>();
        services.AddSingleton<ISettingsSection, AppearanceSettingsSection>();
        services.AddSingleton<ISettingsApi, JsonSettingsApi>();
        services.AddSingleton<ISecureSecretVault, OperatingSystemSecretVault>();
        services.AddSingleton<IConnectorSessionStore, EncryptedConnectorSessionStore>();
        services.AddSingleton<IArchiveExtractionService, SharpCompressArchiveExtractionService>();
        services.AddSingleton<ICompatibilityToolReleaseCatalogService, CompatibilityToolReleaseCatalogService>();
        services.AddSingleton<ICompatibilityToolDiscoveryService, CompatibilityToolDiscoveryService>();
        services.AddSingleton<ICompatibilityToolInstallationService, CompatibilityToolInstallationService>();
        services.AddSingleton<IGamePostInstallService, GamePostInstallService>();
        services.AddSingleton<IGameManagementProvider, ManualGameManagementProvider>();
        services.AddSingleton<IGameManagementService, GameManagementService>();
        services.AddSingleton<IGameLaunchRuntime, NativeGameLaunchRuntime>();
        services.AddSingleton<IGameLaunchRuntimeRegistry, GameLaunchRuntimeRegistry>();
        services.AddSingleton<IGameSourceCatalog, GameSourceCatalog>();
        services.AddSingleton<IGameProcessMonitor, GameProcessMonitor>();
        services.AddSingleton<IGameInstallationStateSyncService, GameInstallationStateSyncService>();
        services.AddSingleton<IHostApplicationLauncher, HostApplicationLauncher>();
        services.AddSingleton<IGameLibrarySyncService, GameLibrarySyncService>();
        services.AddSingleton<IPluginDataStore, PluginDataStore>();

        pluginLoadingOptions ??= new PluginLoadingOptions(Path.Combine(AppContext.BaseDirectory, "plugins"));
        var pluginLoader = new PluginCatalogLoader(pluginLoadingOptions);
        pluginLoader.LoadAndRegister(
            services,
            appDataFileSystem.GetPath("plugins"));

        return services;
    }
}
