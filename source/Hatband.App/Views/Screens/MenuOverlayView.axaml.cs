using Avalonia.Controls;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;

namespace Hatband.App.Views.Screens;

public partial class MenuOverlayView : UserControl
{
    public MenuOverlayView()
    {
        InitializeComponent();
    }

    public event Action<MenuAction>? MenuActionRequested;

    public void FocusSelectedOption()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var option = ItemsHost.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => item.DataContext is MenuOptionViewModel menuOption &&
                                    ReferenceEquals(menuOption, viewModel.MenuOptions[viewModel.SelectedMenuIndex]));
        option?.FocusItem();
    }

    private void OnMenuOptionClick(object? sender, EventArgs e)
    {
        if (sender is ConsoleNavigationItemView { DataContext: MenuOptionViewModel option })
        {
            MenuActionRequested?.Invoke(option.Action);
        }
    }
}
