using Avalonia.Controls;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Screens;

/// <summary>
/// Presents the application menu as a modal and returns actions without managing screen history.
/// </summary>
public partial class MenuOverlayView : ModalView
{
    public MenuOverlayView()
    {
        InitializeComponent();
    }

    protected override Control? GetInitialFocusTarget()
    {
        return ItemsHost.GetVisualDescendants().OfType<Button>().FirstOrDefault();
    }

    private void OnMenuOptionClick(object? sender, EventArgs e)
    {
        if (DataContext is MainMenuViewModel viewModel &&
            sender is Control { DataContext: MenuOptionViewModel option })
        {
            viewModel.Activate(option.Action);
        }
    }

    private void OnMenuOptionFocusEntered(object? sender, EventArgs e)
    {
        if (DataContext is MainMenuViewModel viewModel &&
            sender is Control { DataContext: MenuOptionViewModel selected })
        {
            foreach (var option in viewModel.MenuOptions)
            {
                option.IsSelected = ReferenceEquals(option, selected);
            }
        }
    }
}
