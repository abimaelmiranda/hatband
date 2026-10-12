namespace Hatband.SDK.UI.Connectors;

/// <summary>Contributes a connector panel to the host's Connectors section.</summary>
public interface IConnectorUiProvider
{
    string Id { get; }

    string DisplayName { get; }

    PluginPanel CreatePanel(ConnectorUiContext context);
}
