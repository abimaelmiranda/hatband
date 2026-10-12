using Hatband.Core.Abstractions.Settings;
using Hatband.Plugins.SteamGridDB.Authentication;
using Hatband.Plugins.SteamGridDB.Settings;
using Hatband.Plugins.SteamGridDB.Localization;
using Hatband.SDK.UI.Connectors;

namespace Hatband.Plugins.SteamGridDB.Connectors;

public sealed class SteamGridDBConnectorUiProvider : IConnectorUiProvider
{
    private readonly ISettingsApi settingsApi;
    private readonly SteamGridDBCredentialService credentials;

    public SteamGridDBConnectorUiProvider(ISettingsApi settingsApi, SteamGridDBCredentialService credentials)
    {
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(credentials);
        this.settingsApi = settingsApi;
        this.credentials = credentials;
    }

    public string Id => "steamgriddb";

    public string DisplayName => SteamGridDBResources.PluginName;

    public PluginPanel CreatePanel(ConnectorUiContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SteamGridDBConnectorPanel(settingsApi, credentials);
    }
}
