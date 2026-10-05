using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Hatband.App.Views.Screens;

public partial class SteamConnectorSettingsView : UserControl
{
    public SteamConnectorSettingsView()
    {
        InitializeComponent();
    }

    public void FocusPrimaryAction()
    {
        var control = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => (button.Name is "SteamConnectButton" or "SteamSyncButton") &&
                                      button.IsVisible && button.IsEnabled);
        if (control is not null)
        {
            DirectionalFocusNavigator.Focus(control);
        }
    }
}
