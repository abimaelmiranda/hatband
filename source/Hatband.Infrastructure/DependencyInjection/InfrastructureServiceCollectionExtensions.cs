using Hatband.Core.Abstractions;
using Hatband.Infrastructure.Persistence;
using Hatband.Integrations;
using Hatband.Integrations.Steam;
using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.HowLongToBeat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        var options = new DbContextOptionsBuilder<HatbandDbContext>()
            .UseSqlite(connectionString)
            .Options;

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(25) });
        services.AddSingleton<GameLibraryService>();
        services.AddSingleton<IGameLibraryService>(provider =>
            provider.GetRequiredService<GameLibraryService>());
        services.AddSingleton<IGameArtworkStorage>(
            new FileSystemGameArtworkStorage(appDataFileSystem));
        services.AddSingleton<ISettingsStore>(
            new JsonSettingsStore(appDataFileSystem));
        services.AddSingleton<IGameLibrarySyncService, GameLibrarySyncService>();
        services.AddSingleton<IHowLongToBeatProvider, HowLongToBeatProvider>();
        services.AddSingleton<IGameTimeToBeatSyncService, GameTimeToBeatSyncService>();
        services.AddSingleton<ISteamPlayerService, SteamPlayerService>();
        services.AddSingleton<ISteamInstalledGameScanner, SteamInstalledGameScanner>();
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
            .AddClasses(classes => classes.AssignableTo<IGameMetadataSearchProvider>())
            .As<IGameMetadataSearchProvider>()
            .WithSingletonLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<IntegrationAssemblyMarker>()
            .AddClasses(classes => classes.AssignableTo<IGameArtworkSearchProvider>())
            .As<IGameArtworkSearchProvider>()
            .WithSingletonLifetime());

        return services;
    }
}
