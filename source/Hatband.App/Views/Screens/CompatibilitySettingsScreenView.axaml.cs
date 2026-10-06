using Hatband.App.Views.Navigation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Screens;

public partial class CompatibilitySettingsScreenView : FullScreenView
{
    public CompatibilitySettingsScreenView()
    {
        InitializeComponent();
    }

    public override bool TryHandleBack()
    {
        var viewModel = DataContext as CompatibilitySettingsScreenViewModel;
        if (viewModel is null || !viewModel.IsSaving)
        {
            return false;
        }

        return true;
    }

    protected override Control? GetInitialFocusTarget() =>
        EditorView.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault();
}
