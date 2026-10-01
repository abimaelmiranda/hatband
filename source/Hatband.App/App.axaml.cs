using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using Hatband.App.Services;
using Hatband.App.ViewModels;
using Hatband.App.Views;
using Hatband.Core.Abstractions;
using Hatband.Infrastructure.DependencyInjection;
using Hatband.Infrastructure.Persistence;
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
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Hatband");
            Directory.CreateDirectory(dataDirectory);

            var databasePath = Path.Combine(dataDirectory, "hatband.db");
            var services = new ServiceCollection();
            services.AddLogging(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(new FileLoggerProvider(Path.Combine(dataDirectory, "hatband.log"))));
            services.AddHatbandInfrastructure($"Data Source={databasePath}", dataDirectory);
            services.AddSingleton(new ArtworkImageLoader(dataDirectory));
            services.AddSingleton<DateTimeDisplayFormatter>();
            services.AddSingleton<MainWindowViewModel>();

            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<GameLibraryService>().InitializeDatabase();
            ApplySavedLanguage(
                serviceProvider.GetRequiredService<ISettingsStore>(),
                serviceProvider.GetRequiredService<ILogger<App>>());

            desktop.MainWindow = new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => serviceProvider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplySavedLanguage(ISettingsStore settingsStore, ILogger logger)
    {
        var languageTag = "en-US";

        try
        {
            var settings = Task.Run(() => settingsStore.LoadAsync()).GetAwaiter().GetResult();
            languageTag = settings.General.LanguageTag;
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
