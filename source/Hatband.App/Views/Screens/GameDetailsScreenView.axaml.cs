using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;

namespace Hatband.App.Views.Screens;

public partial class GameDetailsScreenView : UserControl
{
    private readonly ConsoleNavigationItemView[] gameOptionItems;
    private int selectedGameOptionIndex;

    public GameDetailsScreenView()
    {
        InitializeComponent();
        gameOptionItems = [EditGameOptionItem, HiddenGameOptionItem, UninstallGameOptionItem, CompatibilityOptionItem];
        UpdateGameOptionSelection();
    }

    public Button PlayButtonControl => PlayButton;

    public Button OptionsButtonControl => OptionsButton;

    public Control GameOptionsNavigationRoot => GameOptionsOverlay;

    public Control InstallLocationPickerNavigationRoot => InstallLocationPickerOverlay;

    public bool IsInstallLocationPickerControlFocused => InstallLocationComboBox.IsFocused;

    public bool IsGameOptionListItemFocused => GetVisibleGameOptionItems().Any(item => item.HasKeyboardFocus);

    public bool IsOptionsButtonFocused => OptionsButton.IsFocused;

    public bool IsUninstallConfirmationOpen => UninstallConfirmationOverlay.IsVisible;

    public bool IsInstallLocationPickerOpen => InstallLocationPickerOverlay.IsVisible;

    public bool IsInstallLocationPickerDropdownOpen => InstallLocationComboBox.IsDropDownOpen;

    public bool HandleUninstallConfirmationKey(Key key)
    {
        if (!IsUninstallConfirmationOpen)
        {
            return false;
        }

        switch (key)
        {
            case Key.Escape:
                CloseUninstallConfirmation();
                return true;
            case Key.Left or Key.Right:
                if (CancelUninstallButton.IsFocused)
                {
                    DirectionalFocusNavigator.Focus(ConfirmUninstallButton);
                }
                else
                {
                    DirectionalFocusNavigator.Focus(CancelUninstallButton);
                }

                return true;
            case Key.Enter:
                if (ConfirmUninstallButton.IsFocused)
                {
                    _ = ConfirmUninstallAsync();
                }
                else
                {
                    CloseUninstallConfirmation();
                }

                return true;
            default:
                return false;
        }
    }

    public bool HandleInstallLocationPickerKey(Key key)
    {
        if (!IsInstallLocationPickerOpen)
        {
            return false;
        }

        switch (key)
        {
            case Key.Escape:
                if (InstallLocationComboBox.IsDropDownOpen)
                {
                    return false;
                }

                CloseInstallLocationPicker();
                return true;
            case Key.Enter:
                if (InstallLocationComboBox.IsDropDownOpen)
                {
                    return false;
                }

                if (InstallLocationComboBox.IsFocused)
                {
                    InstallLocationComboBox.IsDropDownOpen = true;
                    return true;
                }

                if (ConfirmInstallButton.IsFocused)
                {
                    _ = ConfirmInstallAsync();
                }
                else if (CancelInstallButton.IsFocused)
                {
                    CloseInstallLocationPicker();
                }
                else
                {
                    _ = ConfirmInstallAsync();
                }

                return true;
            default:
                return false;
        }
    }

    public void FocusSelectedGameOption()
    {
        var visibleItems = GetVisibleGameOptionItems();
        selectedGameOptionIndex = Math.Clamp(selectedGameOptionIndex, 0, visibleItems.Length - 1);
        UpdateGameOptionSelection();
        visibleItems[selectedGameOptionIndex].FocusItem();
    }

    public void FocusInstallLocationPicker()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsInstallLocationPickerOpen)
            {
                DirectionalFocusNavigator.Focus(InstallLocationComboBox);
            }
        }, DispatcherPriority.Input);
    }

    public void MoveGameOptionSelection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        var visibleItems = GetVisibleGameOptionItems();
        selectedGameOptionIndex = Math.Clamp(selectedGameOptionIndex + Math.Sign(direction), 0, visibleItems.Length - 1);
        UpdateGameOptionSelection();
        FocusSelectedGameOption();
    }

    public void ActivateSelectedGameOption()
    {
        var visibleItems = GetVisibleGameOptionItems();
        var selectedItem = visibleItems[selectedGameOptionIndex];
        ActivateGameOption(Array.IndexOf(gameOptionItems, selectedItem));
    }

    public event Action? EditRequested;

    public void MoveActionFocus(int direction)
    {
        var focusedIndex = GetFocusedActionIndex();
        var nextIndex = Math.Clamp(focusedIndex + direction, 0, 1);

        switch (nextIndex)
        {
            case 0:
                DirectionalFocusNavigator.Focus(PlayButton);
                break;
            case 1:
                DirectionalFocusNavigator.Focus(OptionsButton);
                break;
        }
    }

    private int GetFocusedActionIndex()
    {
        if (PlayButton.IsFocused)
        {
            return 0;
        }

        if (OptionsButton.IsFocused)
        {
            return 1;
        }

        return 0;
    }

    private void OnEditGameOptionActivated(object? sender, EventArgs e)
    {
        ActivateGameOption(0);
    }

    private void OnHiddenGameOptionActivated(object? sender, EventArgs e)
    {
        ActivateGameOption(1);
    }

    private void OnCompatibilityOptionActivated(object? sender, EventArgs e)
    {
        ActivateGameOption(3);
    }

    private void OnUninstallGameOptionActivated(object? sender, EventArgs e)
    {
        ActivateGameOption(2);
    }

    private void ActivateGameOption(int optionIndex)
    {
        selectedGameOptionIndex = optionIndex;
        UpdateGameOptionSelection();
        switch (optionIndex)
        {
            case 0:
                OnEditGameClick(this, new RoutedEventArgs());
                break;
            case 1:
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.ToggleSelectedGameHiddenCommand.Execute(null);
                }
                break;
            case 2:
                if (DataContext is MainWindowViewModel uninstallViewModel && uninstallViewModel.CanUninstallSelectedGame)
                {
                    uninstallViewModel.IsGameOptionsOpen = false;
                    UninstallConfirmationOverlay.IsVisible = true;
                    Dispatcher.UIThread.Post(() => DirectionalFocusNavigator.Focus(CancelUninstallButton));
                }
                break;
        }
    }

    private void UpdateGameOptionSelection()
    {
        var visibleItems = GetVisibleGameOptionItems();
        selectedGameOptionIndex = Math.Clamp(selectedGameOptionIndex, 0, visibleItems.Length - 1);
        foreach (var item in gameOptionItems)
        {
            item.SetSelected(ReferenceEquals(item, visibleItems[selectedGameOptionIndex]));
        }

    }

    private ConsoleNavigationItemView[] GetVisibleGameOptionItems()
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CanUninstallSelectedGame)
        {
            return gameOptionItems;
        }

        return [EditGameOptionItem, HiddenGameOptionItem, CompatibilityOptionItem];
    }

    private async void OnPlayButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ActivatePrimaryGameActionAsync();
            if (viewModel.IsInstallLocationPickerOpen)
            {
                FocusInstallLocationPicker();
            }
        }
    }

    private void OnOptionsButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ToggleGameOptions();
            if (viewModel.IsGameOptionsOpen)
            {
                FocusSelectedGameOption();
            }
        }
    }

    private void OnCancelUninstallClick(object? sender, RoutedEventArgs e)
    {
        CloseUninstallConfirmation();
    }

    private void OnCancelInstallClick(object? sender, RoutedEventArgs e)
    {
        CloseInstallLocationPicker();
    }

    private async void OnConfirmInstallClick(object? sender, RoutedEventArgs e)
    {
        await ConfirmInstallAsync();
    }

    private void CloseInstallLocationPicker()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsInstallLocationPickerOpen = false;
        }

        DirectionalFocusNavigator.Focus(PlayButton);
    }

    private async Task ConfirmInstallAsync()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.ConfirmGameInstallationAsync();
        DirectionalFocusNavigator.Focus(PlayButton);
    }

    private async void OnConfirmUninstallClick(object? sender, RoutedEventArgs e)
    {
        await ConfirmUninstallAsync();
    }

    private void CloseUninstallConfirmation()
    {
        UninstallConfirmationOverlay.IsVisible = false;
        DirectionalFocusNavigator.Focus(OptionsButton);
    }

    private async Task ConfirmUninstallAsync()
    {
        UninstallConfirmationOverlay.IsVisible = false;
        DirectionalFocusNavigator.Focus(OptionsButton);
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.UninstallSelectedGameAsync();
        }
    }

    private void OnEditGameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.OpenSelectedGameEditor();
        EditRequested?.Invoke();
    }
}
