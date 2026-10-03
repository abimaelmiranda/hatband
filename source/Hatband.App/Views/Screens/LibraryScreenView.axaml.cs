using Avalonia.Controls;
using Avalonia.Interactivity;
using Hatband.App.ViewModels;

namespace Hatband.App.Views.Screens;

public partial class LibraryScreenView : UserControl
{
    public LibraryScreenView()
    {
        InitializeComponent();
    }

    public ListBox GameCarouselControl => GameCarousel;

    public Button EmptyConnectButtonControl => EmptyConnectButton;

    public Button EmptyAddButtonControl => EmptyAddButton;

    public Button EmptyShowAllButtonControl => EmptyShowAllButton;

    public event Action<MenuAction>? MenuActionRequested;

    public event Action? MenuOpened;

    private void OnEmptyPrimaryActionClick(object? sender, RoutedEventArgs e)
    {
        MenuActionRequested?.Invoke(MenuAction.Connectors);
    }

    private void OnEmptyAddGameClick(object? sender, RoutedEventArgs e)
    {
        MenuActionRequested?.Invoke(MenuAction.AddGame);
    }

    private void OnEmptyShowAllGamesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ShowAllGames();
            if (viewModel.IsLibraryEmpty)
            {
                DirectionalFocusNavigator.Focus(EmptyConnectButton);
            }
            else
            {
                DirectionalFocusNavigator.Focus(GameCarousel);
            }
        }
    }
}
