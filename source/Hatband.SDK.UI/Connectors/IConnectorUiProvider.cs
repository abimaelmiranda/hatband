using Hatband.Core.Abstractions.Games;

namespace Hatband.SDK.UI.Connectors;

/// <summary>Contributes a connector panel to the host's Connectors section.</summary>
public interface IConnectorUiProvider : IGameSourceInfo
{
    PluginPanel CreatePanel(ConnectorUiContext context);
}
