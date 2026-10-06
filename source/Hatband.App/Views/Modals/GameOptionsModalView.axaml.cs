using Avalonia.Controls;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Modals;

/// <summary>
/// Presents game actions inside the global modal focus scope instead of an embedded screen overlay.
/// </summary>
public partial class GameOptionsModalView : ModalView
{
    public GameOptionsModalView()
    {
        InitializeComponent();
    }

    protected override Control? GetInitialFocusTarget()
    {
        return OptionsHost.GetVisualDescendants().OfType<Button>().FirstOrDefault();
    }

    private void OnOptionActivated(object? sender, EventArgs e)
    {
        if (DataContext is GameOptionsViewModel viewModel &&
            sender is Control { DataContext: GameOptionViewModel option })
        {
            viewModel.Complete(option.Action);
        }
    }

    private void OnOptionFocusEntered(object? sender, EventArgs e)
    {
        if (sender is not ConsoleNavigationItemView focusedOption)
        {
            return;
        }

        foreach (var option in OptionsHost.GetVisualDescendants().OfType<ConsoleNavigationItemView>())
        {
            option.SetSelected(ReferenceEquals(option, focusedOption));
        }
    }
}
