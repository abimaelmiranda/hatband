using Avalonia.Controls;
using Avalonia.Interactivity;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Screens;

/// <summary>
/// Owns carousel navigation and empty-library actions within the active fullscreen focus scope.
/// </summary>
public partial class LibraryScreenView : FullScreenView
{
    public LibraryScreenView()
    {
        InitializeComponent();
    }

    public override void FocusInitial()
    {
        if (DataContext is not LibraryScreenViewModel viewModel)
        {
            return;
        }

        if (!viewModel.Session.IsLibraryEmpty)
        {
            DirectionalFocusNavigator.Focus(GameCarousel);
            return;
        }

        DirectionalFocusNavigator.Focus(viewModel.Session.IsShowingHiddenGames
            ? EmptyShowAllButton
            : EmptyConnectButton);
    }

    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, NavigationInputContext context)
    {
        if (DataContext is not LibraryScreenViewModel viewModel)
        {
            return NavigationActionHandling.Unhandled;
        }

        if (viewModel.Session.IsLibraryEmpty)
        {
            return base.HandleNavigationAction(action, context);
        }

        switch (action)
        {
            case NavigationAction.Left:
                viewModel.Session.MoveGameSelection(-1);
                return NavigationActionHandling.Handled;
            case NavigationAction.Right:
                viewModel.Session.MoveGameSelection(1);
                return NavigationActionHandling.Handled;
            case NavigationAction.Confirm:
                viewModel.OpenSelectedGame();
                return NavigationActionHandling.Handled;
            default:
                return base.HandleNavigationAction(action, context);
        }
    }

    private void OnEmptyPrimaryActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryScreenViewModel viewModel)
        {
            viewModel.RequestMenuAction(MenuAction.OpenConnectorSettings);
        }
    }

    private void OnEmptyAddGameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryScreenViewModel viewModel)
        {
            viewModel.RequestMenuAction(MenuAction.AddGame);
        }
    }

    private void OnEmptyShowAllGamesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryScreenViewModel viewModel)
        {
            viewModel.Session.ShowAllGames();
            FocusInitial();
        }
    }
}
