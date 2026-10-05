using Hatband.Integrations.Steam.Settings;

namespace Hatband.Integrations.Settings;

public sealed class ConnectorsSettings
{
    public SteamSettings Steam { get; set; } = new();
}
