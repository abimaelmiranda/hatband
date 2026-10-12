using Hatband.Core.Abstractions.Settings;
using Hatband.Plugins.Steam.Store;
using Hatband.SDK.UI.Connectors;

namespace Hatband.Plugins.Steam.Connectors;

public sealed class SteamConnectorUiProvider : IConnectorUiProvider
{
    private readonly SteamStoreIntegration steamStore;
    private readonly ISettingsApi settingsApi;

    public SteamConnectorUiProvider(SteamStoreIntegration steamStore, ISettingsApi settingsApi)
    {
        ArgumentNullException.ThrowIfNull(steamStore);
        ArgumentNullException.ThrowIfNull(settingsApi);
        this.steamStore = steamStore;
        this.settingsApi = settingsApi;
    }

    public string Id => "steam";

    public string DisplayName => "Steam";

    public PluginPanel CreatePanel(ConnectorUiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SteamConnectorPanel(steamStore, settingsApi, context);
    }
}
