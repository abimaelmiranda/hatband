using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;

namespace Hatband.App.Views;

public partial class MainWindow : Window
{
    private readonly DirectionalFocusNavigator directionalFocusNavigator;
    private WindowState previousWindowState;
    private bool isMinimizedForGame;
    private bool isTemporarilyTopmost;
    private bool wasTopmostBeforeRequest;

    public MainWindow()
    {
        InitializeComponent();
        directionalFocusNavigator = new(this);
        LibraryScreenView.MenuActionRequested += ActivateMenuOptionFromScreen;
        LibraryScreenView.MenuOpened += FocusMenuOverlay;
        MenuOverlayView.MenuActionRequested += ActivateMenuOptionFromScreen;
        GameDetailsScreenView.EditRequested += FocusGameEditor;
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.ExitRequested += OnExitRequested;
        viewModel.GameSessionStarted += OnGameSessionStarted;
        viewModel.GameSessionEnded += OnGameSessionEnded;
        viewModel.GameInstallationCompleted += OnGameInstallationCompleted;
        viewModel.GameUninstallationCompleted += OnGameUninstallationCompleted;
        viewModel.SteamFallbackRequested += OnSteamFallbackRequested;
        viewModel.WindowTopmostRequested += OnWindowTopmostRequested;
        viewModel.ReturnToLibraryRequested += OnReturnToLibraryRequested;
        viewModel.GameMetadataEditor.Saved += OnGameMetadataEditorSaved;
        await viewModel.LoadGamesAsync();
        if (viewModel.IsLibraryEmpty)
        {
            FocusWhenVisible(LibraryScreenView.EmptyConnectButtonControl);
        }
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.RefreshPendingInstallationStatesAsync();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExitRequested -= OnExitRequested;
            viewModel.GameSessionStarted -= OnGameSessionStarted;
            viewModel.GameSessionEnded -= OnGameSessionEnded;
            viewModel.GameInstallationCompleted -= OnGameInstallationCompleted;
            viewModel.GameUninstallationCompleted -= OnGameUninstallationCompleted;
            viewModel.SteamFallbackRequested -= OnSteamFallbackRequested;
            viewModel.WindowTopmostRequested -= OnWindowTopmostRequested;
            viewModel.StopPendingInstallationPolling();
            viewModel.StopGameProcessMonitoring();
        }
    }

    private void OnGameSessionStarted(object? sender, EventArgs e)
    {
        if (isMinimizedForGame)
        {
            return;
        }

        previousWindowState = WindowState;
        isMinimizedForGame = true;
        WindowState = WindowState.Minimized;
    }

    private void OnGameSessionEnded(object? sender, EventArgs e)
    {
        if (!isMinimizedForGame)
        {
            return;
        }

        WindowState = previousWindowState;
        isMinimizedForGame = false;
        Activate();
    }

    private void OnGameUninstallationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(Activate);
    }

    private void OnGameInstallationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(Activate);
    }

    private async void OnSteamFallbackRequested(object? sender, EventArgs e)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(700));
        if (IsVisible)
        {
            Dispatcher.UIThread.Post(Activate);
        }
    }

    private void OnWindowTopmostRequested(bool isTopmostRequested)
    {
        if (isTopmostRequested)
        {
            if (isTemporarilyTopmost)
            {
                return;
            }

            wasTopmostBeforeRequest = Topmost;
            isTemporarilyTopmost = true;
            Topmost = true;
            Activate();
            return;
        }

        if (!isTemporarilyTopmost)
        {
            return;
        }

        Topmost = wasTopmostBeforeRequest;
        isTemporarilyTopmost = false;
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsGameSessionActive)
        {
            e.Handled = true;
            return;
        }

        if (viewModel.IsDetailsScreen && GameDetailsScreenView.HandleUninstallConfirmationKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (viewModel.IsDetailsScreen && viewModel.IsInstallLocationPickerOpen)
        {
            if (e.Key == Key.Enter && GameDetailsScreenView.IsInstallLocationPickerDropdownOpen)
            {
                return;
            }

            if (GameDetailsScreenView.HandleInstallLocationPickerKey(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (DirectionalFocusNavigator.IsArrowKey(e.Key))
            {
                if (GameDetailsScreenView.IsInstallLocationPickerControlFocused &&
                    GameDetailsScreenView.IsInstallLocationPickerDropdownOpen &&
                    (e.Key is Key.Up or Key.Down))
                {
                    return;
                }

                e.Handled = directionalFocusNavigator.MoveFocus(
                    GetNavigationRoot(viewModel),
                    e.Key,
                    useNativeArrowBehavior: false);
            }

            return;
        }

        if (viewModel.IsAddGameScreen &&
            await AddGameScreenView.HandleMetadataSearchKeyAsync(e, directionalFocusNavigator))
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (viewModel.IsGameEditorScreen && viewModel.GameMetadataEditor.IsArtworkPickerOpen)
            {
                viewModel.GameMetadataEditor.CloseArtworkPicker();
                FocusWhenVisible(GameMetadataEditorScreenView, GameMetadataEditorScreenView.FocusActiveArtworkSearchButton);
                e.Handled = true;
                return;
            }

            if (viewModel.IsAddGameScreen && viewModel.AddGame.ArtworkPicker.IsArtworkPickerOpen)
            {
                viewModel.AddGame.CloseArtworkSearch();
                FocusWhenVisible(AddGameScreenView, AddGameScreenView.FocusActiveArtworkSearchButton);
                e.Handled = true;
                return;
            }

            if (viewModel.IsSettingsScreen && SettingsScreenView.CloseOpenComboBox())
            {
                e.Handled = true;
                return;
            }

            if (viewModel.IsSettingsScreen && viewModel.IsSettingsContentActive)
            {
                viewModel.DeactivateSettingsContent();
                SettingsScreenView.FocusSelectedSection();
                e.Handled = true;
                return;
            }

            if (viewModel.IsAddGameScreen && viewModel.AddGame.IsContentActive)
            {
                viewModel.AddGame.DeactivateContent();
                AddGameScreenView.FocusSelectedSection();
                e.Handled = true;
                return;
            }

            if (viewModel.IsGameEditorScreen &&
                !viewModel.GameMetadataEditor.IsArtworkPickerOpen &&
                !GameMetadataEditorScreenView.IsEditorSectionFocused)
            {
                GameMetadataEditorScreenView.FocusSelectedEditorSection();
                e.Handled = true;
                return;
            }

            var menuWasOpen = viewModel.IsMenuOpen;
            var gameEditorWasOpen = viewModel.IsGameEditorScreen;
            var gameOptionsWereOpen = viewModel.IsDetailsScreen && viewModel.IsGameOptionsOpen;
            viewModel.GoBack();
            if (viewModel.IsMenuOpen)
            {
                FocusMenuOverlay();
            }
            else if (menuWasOpen)
            {
                FocusCurrentScreen(viewModel);
            }
            else if (gameEditorWasOpen)
            {
                FocusWhenVisible(GameDetailsScreenView.OptionsButtonControl);
            }
            else if (gameOptionsWereOpen)
            {
                FocusWhenVisible(GameDetailsScreenView.OptionsButtonControl);
            }
            else if (viewModel.IsLibraryScreen)
            {
                FocusLibraryScreenTarget(viewModel);
            }

            e.Handled = true;
            return;
        }

        if (DirectionalFocusNavigator.IsTextInput(e.Source, e.Key))
        {
            return;
        }

        if (viewModel.IsGameEditorScreen &&
            viewModel.GameMetadataEditor.IsArtworkPickerOpen &&
            e.Key == Key.Enter &&
            GameMetadataEditorScreenView.IsArtworkOptionFocused)
        {
            GameMetadataEditorScreenView.UseFocusedArtworkOption();
            e.Handled = true;
            return;
        }

        if (viewModel.IsAddGameScreen &&
            viewModel.AddGame.ArtworkPicker.IsArtworkPickerOpen &&
            e.Key == Key.Enter &&
            AddGameScreenView.IsArtworkOptionFocused)
        {
            AddGameScreenView.UseFocusedArtworkOption();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M && e.Source is not TextBox)
        {
            viewModel.ToggleMenu();
            if (viewModel.IsMenuOpen)
            {
                FocusMenuOverlay();
            }

            e.Handled = true;
            return;
        }

        if (viewModel.IsLibraryScreen && viewModel.IsLibraryEmpty && !viewModel.IsMenuOpen)
        {
            if (viewModel.IsShowingHiddenGames)
            {
                if (e.Key is Key.Left or Key.Up or Key.Right or Key.Down ||
                    e.Key == Key.Enter && !LibraryScreenView.EmptyShowAllButtonControl.IsFocused)
                {
                    DirectionalFocusNavigator.Focus(LibraryScreenView.EmptyShowAllButtonControl);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    return;
                }
            }

            if (e.Key is Key.Left or Key.Up)
            {
                DirectionalFocusNavigator.Focus(LibraryScreenView.EmptyConnectButtonControl);
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Right or Key.Down)
            {
                DirectionalFocusNavigator.Focus(LibraryScreenView.EmptyAddButtonControl);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter &&
                !LibraryScreenView.EmptyConnectButtonControl.IsFocused &&
                !LibraryScreenView.EmptyAddButtonControl.IsFocused)
            {
                DirectionalFocusNavigator.Focus(LibraryScreenView.EmptyConnectButtonControl);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                return;
            }
        }

        if (viewModel.IsMenuOpen)
        {
            switch (e.Key)
            {
                case Key.Up:
                    viewModel.MoveMenuSelection(-1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    viewModel.MoveMenuSelection(1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    ActivateMenuOption(viewModel, viewModel.MenuOptions[viewModel.SelectedMenuIndex].Action);
                    e.Handled = true;
                    break;
            }

            return;
        }

        if (viewModel.IsSettingsScreen && e.Key == Key.Enter)
        {
            if (viewModel.IsSettingsContentActive &&
                viewModel.IsSettingsDataSectionSelected &&
                !SettingsScreenView.HasOpenComboBox() &&
                SettingsScreenView.OpenSelectedComboBox())
            {
                e.Handled = true;
                return;
            }

            if (!viewModel.IsSettingsContentActive && SettingsScreenView.IsSectionNavigationFocused)
            {
                viewModel.ActivateSettingsSection();
                if (viewModel.IsSettingsDataSectionSelected)
                {
                    FocusWhenVisible(SettingsScreenView, SettingsScreenView.FocusSelectedSettingField);
                }
                else if (viewModel.IsCompatibilitySettingsSection)
                {
                    FocusWhenVisible(
                        SettingsScreenView,
                        SettingsScreenView.FocusCompatibilityRefreshButton);
                }

                e.Handled = true;
                return;
            }
        }

        if (viewModel.IsAddGameScreen &&
            (e.Key is Key.Enter or Key.Right) &&
            !viewModel.AddGame.IsContentActive &&
            AddGameScreenView.IsSectionNavigationFocused)
        {
            viewModel.AddGame.ActivateContent();
            AddGameScreenView.FocusSelectedField();
            e.Handled = true;
            return;
        }

        if (viewModel.IsGameEditorScreen && !viewModel.GameMetadataEditor.IsArtworkPickerOpen)
        {
            if (GameMetadataEditorScreenView.IsEditorSectionFocused && (e.Key is Key.Enter or Key.Right))
            {
                GameMetadataEditorScreenView.ActivateSectionContent();
                FocusWhenVisible(
                    GameMetadataEditorScreenView,
                    GameMetadataEditorScreenView.FocusEditorSectionContent);
                if (e.Key == Key.Enter)
                {
                    GameMetadataEditorScreenView.SearchSelectedMetadataSource();
                }

                e.Handled = true;
                return;
            }

        }

        if (viewModel.IsDetailsScreen && viewModel.IsGameOptionsOpen)
        {
            if (GameDetailsScreenView.IsGameOptionListItemFocused && (e.Key is Key.Up or Key.Down))
            {
                GameDetailsScreenView.MoveGameOptionSelection(e.Key == Key.Up ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (GameDetailsScreenView.IsGameOptionListItemFocused && e.Key == Key.Enter)
            {
                GameDetailsScreenView.ActivateSelectedGameOption();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Left or Key.Right)
            {
                e.Handled = true;
                return;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Left when viewModel.IsLibraryScreen:
                viewModel.MoveGameSelection(-1);
                e.Handled = true;
                break;
            case Key.Right when viewModel.IsLibraryScreen:
                viewModel.MoveGameSelection(1);
                e.Handled = true;
                break;
            case Key.Enter when viewModel.IsLibraryScreen:
                viewModel.OpenSelectedGame();
                FocusWhenVisible(GameDetailsScreenView.PlayButtonControl);
                e.Handled = true;
                break;
            case Key.Up or Key.Left when viewModel.IsDetailsScreen:
                GameDetailsScreenView.MoveActionFocus(-1);
                e.Handled = true;
                break;
            case Key.Down or Key.Right when viewModel.IsDetailsScreen:
                GameDetailsScreenView.MoveActionFocus(1);
                e.Handled = true;
                break;
            case Key.Enter when viewModel.IsDetailsScreen:
                if (GameDetailsScreenView.IsOptionsButtonFocused)
                {
                    viewModel.ToggleGameOptions();
                    if (viewModel.IsGameOptionsOpen)
                    {
                        FocusWhenVisible(GameDetailsScreenView, GameDetailsScreenView.FocusSelectedGameOption);
                    }
                }
                else
                {
                    await viewModel.ActivatePrimaryGameActionAsync();
                    if (viewModel.IsInstallLocationPickerOpen)
                    {
                        GameDetailsScreenView.FocusInstallLocationPicker();
                    }
                }

                e.Handled = true;
                break;
        }

    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnReturnToLibraryRequested(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            FocusLibraryScreenTarget(viewModel);
        }
    }

    private void OnGameMetadataEditorSaved(object? sender, Game game)
    {
        FocusWhenVisible(GameDetailsScreenView.OptionsButtonControl);
    }

    private void FocusGameEditor()
    {
        FocusWhenVisible(GameMetadataEditorScreenView, GameMetadataEditorScreenView.FocusSelectedEditorSection);
    }

    private void FocusMenuOverlay()
    {
        FocusWhenVisible(MenuOverlayView, MenuOverlayView.FocusSelectedOption);
    }

    private void FocusCurrentScreen(MainWindowViewModel viewModel)
    {
        if (viewModel.IsLibraryScreen)
        {
            FocusLibraryScreenTarget(viewModel);
            return;
        }

        if (viewModel.IsDetailsScreen)
        {
            if (viewModel.IsGameOptionsOpen)
            {
                FocusWhenVisible(GameDetailsScreenView, GameDetailsScreenView.FocusSelectedGameOption);
            }
            else
            {
                FocusWhenVisible(GameDetailsScreenView.PlayButtonControl);
            }
            return;
        }

        if (viewModel.IsSettingsScreen)
        {
            if (viewModel.IsSettingsContentActive)
            {
                SettingsScreenView.FocusSelectedSettingField();
                return;
            }

            SettingsScreenView.FocusSelectedSection();
            return;
        }

        if (viewModel.IsGameEditorScreen)
        {
            FocusWhenVisible(GameMetadataEditorScreenView, GameMetadataEditorScreenView.FocusSelectedEditorSection);
            return;
        }

        if (viewModel.IsAddGameScreen)
        {
            if (viewModel.AddGame.IsContentActive)
            {
                AddGameScreenView.FocusSelectedField();
            }
            else
            {
                FocusWhenVisible(AddGameScreenView, AddGameScreenView.FocusSelectedSection);
            }
        }
    }

    private void ActivateMenuOption(MainWindowViewModel viewModel, MenuAction action)
    {
        viewModel.ActivateMenuOption(action);
        if (action == MenuAction.AddGame)
        {
            FocusWhenVisible(AddGameScreenView, AddGameScreenView.FocusSelectedSection);
        }
        else if (action == MenuAction.OpenConnectorSettings)
        {
            viewModel.ActivateSettingsSection();
            FocusWhenVisible(SettingsScreenView, SettingsScreenView.FocusConnectorsPrimaryAction);
        }
        else if (action == MenuAction.Settings)
        {
            FocusWhenVisible(SettingsScreenView, SettingsScreenView.FocusSelectedSection);
        }
        else if (action is MenuAction.Library or MenuAction.HiddenGames)
        {
            FocusLibraryScreenTarget(viewModel);
        }
    }

    private void ActivateMenuOptionFromScreen(MenuAction action)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            ActivateMenuOption(viewModel, action);
        }
    }

    private void FocusLibraryScreenTarget(MainWindowViewModel viewModel)
    {
        if (!viewModel.IsLibraryEmpty)
        {
            FocusWhenVisible(LibraryScreenView.GameCarouselControl);
            return;
        }

        FocusWhenVisible(viewModel.IsShowingHiddenGames
            ? LibraryScreenView.EmptyShowAllButtonControl
            : LibraryScreenView.EmptyConnectButtonControl);
    }

    private void FocusWhenVisible(Control control)
    {
        EventHandler? onLayoutUpdated = null;
        onLayoutUpdated = (_, _) =>
        {
            if (!control.IsEffectivelyVisible)
            {
                return;
            }

            LayoutUpdated -= onLayoutUpdated;
            DirectionalFocusNavigator.Focus(control);
        };

        LayoutUpdated += onLayoutUpdated;
        Dispatcher.UIThread.Post(() =>
        {
            if (!control.IsEffectivelyVisible)
            {
                return;
            }

            LayoutUpdated -= onLayoutUpdated;
            DirectionalFocusNavigator.Focus(control);
        }, DispatcherPriority.Background);
    }

    private void FocusWhenVisible(Control owner, Action focusAction)
    {
        EventHandler? onLayoutUpdated = null;
        onLayoutUpdated = (_, _) =>
        {
            if (!owner.IsEffectivelyVisible)
            {
                return;
            }

            LayoutUpdated -= onLayoutUpdated;
            focusAction();
        };

        LayoutUpdated += onLayoutUpdated;
        Dispatcher.UIThread.Post(() =>
        {
            if (!owner.IsEffectivelyVisible)
            {
                return;
            }

            LayoutUpdated -= onLayoutUpdated;
            focusAction();
        }, DispatcherPriority.Background);
    }

    private Control GetNavigationRoot(MainWindowViewModel viewModel)
    {
        if (viewModel.IsMenuOpen)
        {
            return MenuOverlayView;
        }

        if (viewModel.IsGameEditorScreen)
        {
            if (viewModel.GameMetadataEditor.IsArtworkPickerOpen)
            {
                return GameMetadataEditorScreenView.ArtworkPickerNavigationRoot;
            }

            return GameMetadataEditorScreenView;
        }

        if (viewModel.IsAddGameScreen)
        {
            if (viewModel.AddGame.IsMetadataSearchOpen)
            {
                return AddGameScreenView.MetadataSearchNavigationRoot;
            }

            if (viewModel.AddGame.ArtworkPicker.IsArtworkPickerOpen)
            {
                return AddGameScreenView.ArtworkPickerNavigationRoot;
            }

            return AddGameScreenView;
        }

        if (viewModel.IsSettingsScreen)
        {
            return SettingsScreenView;
        }

        if (viewModel.IsDetailsScreen)
        {
            if (viewModel.IsInstallLocationPickerOpen)
            {
                return GameDetailsScreenView.InstallLocationPickerNavigationRoot;
            }

            if (viewModel.IsGameOptionsOpen)
            {
                return GameDetailsScreenView.GameOptionsNavigationRoot;
            }

            return GameDetailsScreenView;
        }

        return LibraryScreenView;
    }

}
