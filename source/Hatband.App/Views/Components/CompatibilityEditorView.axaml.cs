using Avalonia.Controls;
using Avalonia.Interactivity;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Components;

public partial class CompatibilityEditorView : UserControl
{
    public CompatibilityEditorView()
    {
        InitializeComponent();
    }

    private void OnOriginalClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompatibilityEditorViewModel viewModel)
        {
            viewModel.SetOriginalMode();
            viewModel.ErrorMessage = null;
        }
    }

    private void OnCustomClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompatibilityEditorViewModel viewModel)
        {
            viewModel.SetCustomMode();
            viewModel.ErrorMessage = null;
        }
    }

    private void OnRemoveEnvironmentVariableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompatibilityEditorViewModel viewModel &&
            sender is Control { DataContext: EnvironmentVariableViewModel variable })
        {
            viewModel.EnvironmentVariables.Remove(variable);
        }
    }
}
