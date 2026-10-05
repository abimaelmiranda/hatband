using Avalonia.Controls;
using Avalonia.Interactivity;
using Hatband.App.ViewModels;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Modals;

/// <summary>
/// Presents a destructive-action confirmation with Cancel as its safe initial focus target.
/// Input isolation and owner restoration are provided by the global modal host.
/// </summary>
public partial class ConfirmationModalView : ModalView
{
    public ConfirmationModalView()
    {
        InitializeComponent();
    }

    protected override Control? GetInitialFocusTarget() => CancelButton;

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConfirmationModalViewModel viewModel)
        {
            viewModel.Cancel();
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConfirmationModalViewModel viewModel)
        {
            viewModel.Complete(true);
        }
    }
}
