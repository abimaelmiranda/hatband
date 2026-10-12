using System.Globalization;
using System.Resources;

namespace Hatband.Plugins.Steam.Localization;

public static class SteamResources
{
    private static readonly ResourceManager ResourceManager =
        new("Hatband.Plugins.Steam.Localization.SteamResources", typeof(SteamResources).Assembly);

    public static string SilentMode => Get(nameof(SilentMode));
    public static string SilentModeDescription => Get(nameof(SilentModeDescription));
    public static string ConnectSteamAccount => Get(nameof(ConnectSteamAccount));
    public static string ConnectDescription => Get(nameof(ConnectDescription));
    public static string ScanQrDescription => Get(nameof(ScanQrDescription));
    public static string CancelConnection => Get(nameof(CancelConnection));
    public static string Connected => Get(nameof(Connected));
    public static string ConnectedAs => Get(nameof(ConnectedAs));
    public static string NotConnected => Get(nameof(NotConnected));
    public static string SyncLibrary => Get(nameof(SyncLibrary));
    public static string DisconnectAccount => Get(nameof(DisconnectAccount));
    public static string Connecting => Get(nameof(Connecting));
    public static string ScanQr => Get(nameof(ScanQr));
    public static string SessionTemporary => Get(nameof(SessionTemporary));
    public static string LoginCancelled => Get(nameof(LoginCancelled));
    public static string LoginSyncFailed => Get(nameof(LoginSyncFailed));
    public static string SyncingLibrary => Get(nameof(SyncingLibrary));
    public static string SyncComplete => Get(nameof(SyncComplete));
    public static string SyncFailed => Get(nameof(SyncFailed));
    public static string AccountDisconnected => Get(nameof(AccountDisconnected));
    public static string DisconnectFailed => Get(nameof(DisconnectFailed));

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Steam localization resource '{key}' is missing.");
}
