using Avalonia.Controls;
using Avalonia.Interactivity;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Modals;

/// <summary>
/// Presents installation locations and handles the native dropdown before closing the modal on Back.
/// </summary>
public partial class InstallLocationModalView : ModalView
{
    public InstallLocationModalView()
    {
        InitializeComponent();
    }

    protected override Control? GetInitialFocusTarget() => LocationComboBox;

    public override bool TryHandleBack()
    {
        if (!LocationComboBox.IsDropDownOpen)
        {
            return false;
        }

        LocationComboBox.IsDropDownOpen = false;
        return true;
    }

    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, NavigationInputContext context)
    {
        if (LocationComboBox.IsDropDownOpen)
        {
            return NavigationActionHandling.Native;
        }

        if (action == NavigationAction.Confirm && LocationComboBox.IsFocused)
        {
            LocationComboBox.IsDropDownOpen = true;
            return NavigationActionHandling.Handled;
        }

        return base.HandleNavigationAction(action, context);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstallLocationModalViewModel viewModel)
        {
            viewModel.Cancel();
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstallLocationModalViewModel viewModel)
        {
            viewModel.Complete(viewModel.SelectedLocation);
        }
    }
}
