using Avalonia.Controls;
using Avalonia.Interactivity;
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
        gameOptionItems = [EditGameOptionItem, HiddenGameOptionItem, CompatibilityOptionItem];
        UpdateGameOptionSelection();
    }

    public Button PlayButtonControl => PlayButton;

    public Button OptionsButtonControl => OptionsButton;

    public Control GameOptionsNavigationRoot => GameOptionsOverlay;

    public bool IsGameOptionListItemFocused => gameOptionItems.Any(item => item.HasKeyboardFocus);

    public bool IsOptionsButtonFocused => OptionsButton.IsFocused;

    public void FocusSelectedGameOption()
    {
        gameOptionItems[selectedGameOptionIndex].FocusItem();
    }

    public void MoveGameOptionSelection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        selectedGameOptionIndex = Math.Clamp(
            selectedGameOptionIndex + Math.Sign(direction),
            0,
            gameOptionItems.Length - 1);
        UpdateGameOptionSelection();
        FocusSelectedGameOption();
    }

    public void ActivateSelectedGameOption()
    {
        ActivateGameOption(selectedGameOptionIndex);
    }

    public event Action? EditRequested;

    public void MoveActionFocus(int direction)
    {
        var focusedIndex = GetFocusedActionIndex();
        var nextIndex = Math.Clamp(focusedIndex + direction, 0, 1);

        switch (nextIndex)
        {
            case 0:
                PlayButton.Focus();
                break;
            case 1:
                OptionsButton.Focus();
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
        }
    }

    private void UpdateGameOptionSelection()
    {
        for (var index = 0; index < gameOptionItems.Length; index++)
        {
            gameOptionItems[index].SetSelected(index == selectedGameOptionIndex);
        }

    }

    private void OnPlayButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ActivatePrimaryGameAction();
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
