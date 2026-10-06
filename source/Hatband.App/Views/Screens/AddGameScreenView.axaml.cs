using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;
using Hatband.App.Views.Navigation;
using Hatband.Core.Models.Games;
using AppResources = Hatband.App.Localization.Resources;

namespace Hatband.App.Views.Screens;

/// <summary>Hosts manual game entry and keeps section/content focus within the editor.</summary>
public partial class AddGameScreenView : FullScreenView
{
    public AddGameScreenView()
    {
        InitializeComponent();
    }

    public bool IsSectionNavigationFocused => AddGameSectionList.GetVisualDescendants()
        .OfType<Button>()
        .Any(button => button.IsFocused);

    public Control ArtworkPickerNavigationRoot => ImagesPanel;

    public bool IsArtworkOptionFocused => GetActiveArtworkList() is { } activeList &&
        (activeList.IsFocused || activeList.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Any(item => item.IsFocused));

    public void FocusSelectedArtworkOption()
    {
        var activeList = GetActiveArtworkList();
        if (activeList is null)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(activeList);
        Dispatcher.UIThread.Post(() =>
        {
            if (activeList.SelectedItem is not GameArtworkSourceOption selectedOption)
            {
                return;
            }

            var selectedItem = activeList.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, selectedOption));
            if (selectedItem is not null)
            {
                DirectionalFocusNavigator.Focus(selectedItem);
            }
        });
    }

    public void FocusActiveArtworkSearchButton()
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        var button = viewModel.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? SearchCoverArtworkButton
            : SearchBackgroundArtworkButton;
        DirectionalFocusNavigator.Focus(button);
    }

    public void FocusSelectedSection()
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        var section = AddGameSectionList.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.SelectedSection));
        section?.FocusItem();
    }

    public void FocusSelectedField()
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        var fields = GetFocusableEditorFields();
        var fieldIndex = viewModel.SelectedFieldIndex;
        if ((uint)fieldIndex < (uint)fields.Count)
        {
            DirectionalFocusNavigator.Focus(fields[fieldIndex]);
        }
    }

    /// <summary>Handles local Back transitions and leaves inline artwork arrows to the selected list.</summary>
    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, KeyEventArgs originalEvent)
    {
        if (action == NavigationAction.Back)
        {
            return TryHandleBack(originalEvent)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        if (action == NavigationAction.Confirm &&
            DataContext is AddGameViewModel addGameViewModel &&
            addGameViewModel.ArtworkPicker.IsArtworkPickerOpen &&
            IsArtworkOptionFocused)
        {
            originalEvent.Handled = true;
            UseFocusedArtworkOption();
            return NavigationActionHandling.Handled;
        }

        if ((action is NavigationAction.Confirm or NavigationAction.Right) &&
            DataContext is AddGameViewModel sectionViewModel &&
            !sectionViewModel.IsContentActive && IsSectionNavigationFocused)
        {
            originalEvent.Handled = true;
            sectionViewModel.ActivateContent();
            FocusSelectedField();
            return NavigationActionHandling.Handled;
        }

        if ((action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right) &&
            DataContext is AddGameViewModel viewModel &&
            viewModel.ArtworkPicker.IsArtworkPickerOpen &&
            GetActiveArtworkList() is not null)
        {
            return NavigationActionHandling.Native;
        }

        return base.HandleNavigationAction(action, originalEvent);
    }

    /// <summary>Moves from editor fields to the section rail before requesting shell cancellation.</summary>
    public override bool TryHandleBack(KeyEventArgs originalEvent)
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return false;
        }

        originalEvent.Handled = true;
        if (viewModel.ArtworkPicker.IsArtworkPickerOpen)
        {
            var slot = viewModel.ArtworkPicker.ActiveArtworkSlot;
            viewModel.CloseArtworkSearch();
            DirectionalFocusNavigator.Focus(slot == GameArtworkSlot.Cover
                ? SearchCoverArtworkButton
                : SearchBackgroundArtworkButton);
            return true;
        }

        if (viewModel.IsContentActive)
        {
            viewModel.DeactivateContent();
            FocusSelectedSection();
            return true;
        }

        viewModel.CancelCommand.Execute(null);
        return true;
    }

    protected override Control? GetInitialFocusTarget()
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return null;
        }

        if (viewModel.IsContentActive)
        {
            var fields = GetFocusableEditorFields();
            if ((uint)viewModel.SelectedFieldIndex < (uint)fields.Count)
            {
                return fields[viewModel.SelectedFieldIndex];
            }
        }

        var selectedSectionItem = AddGameSectionList.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.SelectedSection));
        return selectedSectionItem?.GetVisualDescendants().OfType<Button>().FirstOrDefault();
    }

    private async void OnChooseExecutableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel ||
            sender is not Control { DataContext: AddGameActionViewModel action })
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            viewModel.StatusMessage = AppResources.FilePickerError;
            return;
        }

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppResources.SelectExecutableTitle,
                AllowMultiple = false
            });

            if (files.Count == 0)
            {
                return;
            }

            var executablePath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                viewModel.StatusMessage = AppResources.ChooseLocalFile;
                return;
            }

            action.Type = GameActionType.Executable;
            action.Target = executablePath;
            action.WorkingDirectory ??= Path.GetDirectoryName(executablePath);
            viewModel.InstallDirectory ??= Path.GetDirectoryName(executablePath);
            viewModel.StatusMessage = null;
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = string.Format(AppResources.SelectExecutableError, exception.Message);
        }
    }

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            viewModel.StatusMessage = AppResources.FilePickerError;
            return;
        }

        try
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = AppResources.ChooseFolder,
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                return;
            }

            var folderPath = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                viewModel.StatusMessage = AppResources.ChooseLocalFolder;
                return;
            }

            if (sender is Control { DataContext: AddGameActionViewModel action })
            {
                action.WorkingDirectory = folderPath;
            }
            else
            {
                viewModel.InstallDirectory = folderPath;
            }

            viewModel.StatusMessage = null;
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = string.Format(AppResources.SelectFolderError, exception.Message);
        }
    }

    private void OnSectionFocusEntered(object? sender, EventArgs e)
    {
        if (DataContext is AddGameViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: AddGameSectionViewModel section })
        {
            viewModel.SelectSection(section);
        }
    }

    private void OnSectionActivated(object? sender, EventArgs e)
    {
        if (DataContext is AddGameViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: AddGameSectionViewModel section })
        {
            viewModel.SelectSection(section);
            viewModel.ActivateContent();
            FocusSelectedField();
        }
    }

    private async void OnOpenMetadataSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AddGameViewModel viewModel)
        {
            e.Handled = true;
            await viewModel.OpenMetadataSearchAsync();
            FocusMetadataSearchButton();
        }
    }

    private void FocusMetadataSearchButton() =>
        DirectionalFocusNavigator.Focus(OpenMetadataSearchButton);

    private void OnAddActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        viewModel.AddActionCommand.Execute(null);
        var addedAction = viewModel.Actions.Last();
        Dispatcher.UIThread.Post(() =>
        {
            var nameInput = ActionsPanel.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(input => ReferenceEquals(input.DataContext, addedAction));
            if (nameInput is not null)
            {
                DirectionalFocusNavigator.Focus(nameInput);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnEditorFieldGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel || e.Source is not Control focusedControl)
        {
            return;
        }

        var fields = GetFocusableEditorFields();
        var focusedField = fields.IndexOf(focusedControl);
        if (focusedField >= 0)
        {
            viewModel.SelectField(focusedField);
        }
    }

    private List<Control> GetFocusableEditorFields()
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return [];
        }

        var activePanel = viewModel.SelectedSection.Id switch
        {
            AddGameViewModel.GameSectionId => GamePanel,
            AddGameViewModel.ActionsSectionId => ActionsPanel,
            AddGameViewModel.ImagesSectionId => ImagesPanel,
            AddGameViewModel.CompatibilitySectionId => CompatibilityPanel,
            _ => throw new InvalidOperationException($"Unknown add-game section '{viewModel.SelectedSection.Id}'.")
        };
        return activePanel.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEnabled &&
                              control.Focusable &&
                              control.IsTabStop &&
                              control.IsEffectivelyVisible)
            .ToList();
    }

    private async void OnSearchArtworkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel ||
            sender is not Button { Tag: string slotName })
        {
            return;
        }

        var slot = slotName switch
        {
            "Cover" => GameArtworkSlot.Cover,
            "Background" => GameArtworkSlot.Background,
            _ => throw new ArgumentOutOfRangeException(nameof(slotName), slotName, "Unknown artwork slot.")
        };
        e.Handled = true;
        await viewModel.SearchArtworkAsync(slot);
        FocusSelectedArtworkOption();
    }

    private void OnUseSelectedArtworkClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        UseFocusedArtworkOption();
    }

    /// <summary>Applies the preview selected in the inline image section and returns focus to its search button.</summary>
    public void UseFocusedArtworkOption()
    {
        if (DataContext is not AddGameViewModel viewModel ||
            !viewModel.ArtworkPicker.CanApplyActiveArtwork)
        {
            return;
        }

        viewModel.ApplySelectedArtwork();
        FocusActiveArtworkSearchButton();
    }

    private void OnCloseArtworkPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddGameViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        var slot = viewModel.ArtworkPicker.ActiveArtworkSlot;
        viewModel.CloseArtworkSearch();
        DirectionalFocusNavigator.Focus(slot == GameArtworkSlot.Cover
            ? SearchCoverArtworkButton
            : SearchBackgroundArtworkButton);
    }

    private ListBox? GetActiveArtworkList()
    {
        if (DataContext is not AddGameViewModel viewModel ||
            !viewModel.ArtworkPicker.IsArtworkPickerOpen)
        {
            return null;
        }

        return viewModel.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? CoverArtworkOptions
            : BackgroundArtworkOptions;
    }
}
