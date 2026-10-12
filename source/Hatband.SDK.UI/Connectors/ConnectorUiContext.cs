namespace Hatband.SDK.UI.Connectors;

/// <summary>Host operations available to a connector panel.</summary>
public sealed record ConnectorUiContext(
    Func<string, CancellationToken, Task> SyncAsync);
