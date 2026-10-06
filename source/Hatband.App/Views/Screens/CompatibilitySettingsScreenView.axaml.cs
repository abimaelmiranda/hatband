using Hatband.App.Views.Navigation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Screens;

public partial class CompatibilitySettingsScreenView : FullScreenView
{
    public CompatibilitySettingsScreenView()
    {
        InitializeComponent();
    }

    public override bool TryHandleBack(KeyEventArgs originalEvent)
    {
        var viewModel = DataContext as CompatibilitySettingsScreenViewModel;
        if (viewModel is null || !viewModel.IsSaving)
        {
            return false;
        }

        originalEvent.Handled = true;
        return true;
    }

    protected override Control? GetInitialFocusTarget() =>
        EditorView.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault();
}
