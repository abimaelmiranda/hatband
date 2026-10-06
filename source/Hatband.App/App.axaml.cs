using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using Hatband.App.Services;
using Hatband.App.Services.Input;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.ViewModels.Settings;
using Hatband.App.Views;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.DependencyInjection;
using Hatband.Infrastructure.FileSystem;
using Hatband.Infrastructure.Persistence;
using Hatband.Infrastructure.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hatband.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var hostSystemInfo = new HostSystemInfo();
            var appDataFileSystem = new AppDataFileSystem(hostSystemInfo);
            var databasePath = appDataFileSystem.GetPath("hatband.db");
            var services = new ServiceCollection();
            services.AddSingleton<IHostSystemInfo>(hostSystemInfo);
            services.AddSingleton<IAppDataFileSystem>(appDataFileSystem);
            services.AddLogging(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(new FileLoggerProvider(appDataFileSystem.GetPath("hatband.log"))));
            services.AddHatbandInfrastructure($"Data Source={databasePath}", appDataFileSystem);
            services.AddSingleton<GamepadInputService>();
            services.AddSingleton<ArtworkImageLoader>();
            services.AddSingleton<DateTimeDisplayFormatter>();
            services.AddSingleton<GameProcessSessionService>();
            services.AddSingleton<ProtonManagementViewModel>();
            services.AddSingleton<SettingsScreenViewModel>();
            services.AddSingleton<NavigationCoordinator>();
            services.AddSingleton<IScreenNavigation>(provider => provider.GetRequiredService<NavigationCoordinator>());
            services.AddSingleton<IModalService>(provider => provider.GetRequiredService<NavigationCoordinator>());
            services.AddSingleton<LibrarySessionViewModel>();
            services.AddSingleton<LibraryScreenViewModel>();
            services.AddSingleton<GameDetailsScreenViewModel>();
            services.AddTransient<CompatibilityEditorViewModel>();
            services.AddSingleton<CompatibilitySettingsScreenViewModel>();
            services.AddSingleton<AddGameViewModel>();
            services.AddSingleton<GameMetadataEditorViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
            ApplySavedLanguage(
                serviceProvider.GetRequiredService<ISettingsApi>(),
                serviceProvider.GetRequiredService<ILogger<App>>());

            desktop.MainWindow = new MainWindow(
                serviceProvider.GetRequiredService<GamepadInputService>(),
                serviceProvider.GetRequiredService<SettingsScreenViewModel>())
            {
                DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => serviceProvider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplySavedLanguage(ISettingsApi settingsApi, ILogger logger)
    {
        var languageTag = "en-US";

        try
        {
            var settings = Task.Run(() => settingsApi.GetSectionAsync<GeneralSettings>()).GetAwaiter().GetResult();
            languageTag = settings.LanguageTag;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not load the saved language preference.");
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(languageTag);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo("en-US");
        }

        Hatband.App.Localization.Resources.Culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
