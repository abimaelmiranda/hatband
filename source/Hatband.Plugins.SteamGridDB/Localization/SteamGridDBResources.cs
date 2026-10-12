using System.Globalization;
using System.Resources;

namespace Hatband.Plugins.SteamGridDB.Localization;

internal static class SteamGridDBResources
{
    private static readonly ResourceManager ResourceManager = new(
        "Hatband.Plugins.SteamGridDB.Localization.Resources",
        typeof(SteamGridDBResources).Assembly);

    public static string PluginName => Get(nameof(PluginName));
    public static string ModeTitle => Get(nameof(ModeTitle));
    public static string ScraperMode => Get(nameof(ScraperMode));
    public static string ApiMode => Get(nameof(ApiMode));
    public static string ModeDescription => Get(nameof(ModeDescription));
    public static string KeyTitle => Get(nameof(KeyTitle));
    public static string KeyWatermark => Get(nameof(KeyWatermark));
    public static string KeyConfigured => Get(nameof(KeyConfigured));
    public static string KeyMissing => Get(nameof(KeyMissing));
    public static string SaveKey => Get(nameof(SaveKey));
    public static string DeleteKey => Get(nameof(DeleteKey));
    public static string ModeSaved => Get(nameof(ModeSaved));
    public static string ModeSaveFailed => Get(nameof(ModeSaveFailed));
    public static string KeySaved => Get(nameof(KeySaved));
    public static string KeyDeleted => Get(nameof(KeyDeleted));
    public static string KeySaveFailed => Get(nameof(KeySaveFailed));
    public static string KeyDeleteFailed => Get(nameof(KeyDeleteFailed));
    public static string KeyRequired => Get(nameof(KeyRequired));
    public static string KeyStoreUnavailable => Get(nameof(KeyStoreUnavailable));
    public static string SettingsUnavailable => Get(nameof(SettingsUnavailable));
    public static string MissingKeyForApiMode => Get(nameof(MissingKeyForApiMode));
    public static string EmptyApiKey => Get(nameof(EmptyApiKey));
    public static string InvalidMode => Get(nameof(InvalidMode));
    public static string ApiKeyRejected => Get(nameof(ApiKeyRejected));
    public static string ApiRedirected => Get(nameof(ApiRedirected));
    public static string InvalidApiKey => Get(nameof(InvalidApiKey));

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing SteamGridDB resource: {name}.");
}
