using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Screens;

public partial class ConnectorsScreenView : UserControl
{
    public ConnectorsScreenView()
    {
        InitializeComponent();
    }

    public ItemsControl ConnectorListControl => ConnectorList;

    public bool IsConnectorListItemFocused => ConnectorList.GetVisualDescendants()
        .OfType<Button>()
        .Any(button => button.IsFocused);

    public void FocusSelectedConnector()
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.Connectors.Count == 0)
        {
            return;
        }

        var connectorButton = ConnectorList.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.DataContext is ConnectorViewModel connector &&
                                      ReferenceEquals(connector, viewModel.Connectors[viewModel.SelectedConnectorIndex]));
        connectorButton?.Focus();
    }

    public Button SteamConnectButtonControl => SteamConnectButton;

    public Button SteamSyncButtonControl => SteamSyncButton;

    public Control GetConnectorAction(bool isConnected, bool isSteam)
    {
        if (isSteam)
        {
            return SteamSilentModeCheckBox;
        }

        if (isConnected)
        {
            return SteamSyncButton;
        }

        return SteamConnectButton;
    }

    public event Action<ConnectorViewModel>? ConnectorOpened;

    private void OnConnectorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Button { DataContext: ConnectorViewModel connector })
        {
            return;
        }

        viewModel.OpenConnector(connector);
        ConnectorOpened?.Invoke(connector);
    }
}
