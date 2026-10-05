using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;
using Hatband.Core.Models.Games;
using AppResources = Hatband.App.Localization.Resources;

namespace Hatband.App.Views.Screens;

public partial class AddGameScreenView : UserControl
{
    public AddGameScreenView()
    {
        InitializeComponent();
    }

    public bool IsSectionNavigationFocused => AddGameSectionList.GetVisualDescendants()
        .OfType<Button>()
        .Any(button => button.IsFocused);

    public Control ArtworkPickerNavigationRoot => ImagesPanel;

    public Control MetadataSearchNavigationRoot => MetadataSearchOverlay;

    private bool IsMetadataSearchQueryFocused => MetadataSearchQueryBox.IsFocused;

    private bool IsMetadataSearchRunButtonFocused => MetadataSearchRunButton.IsFocused;

    private bool IsMetadataSearchApplyButtonFocused => ApplyMetadataSearchButton.IsFocused;

    private bool IsMetadataSearchCloseButtonFocused => CloseMetadataSearchButton.IsFocused;

    private bool IsMetadataSearchCancelButtonFocused => CancelMetadataSearchButton.IsFocused;

    private bool IsMetadataSearchResultsFocused => GetMetadataSearchResultsList() is { } listBox &&
        (listBox.IsFocused || listBox.GetVisualDescendants()
            .OfType<Control>()
            .Any(control => control.IsFocused));

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
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var button = viewModel.AddGame.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? SearchCoverArtworkButton
            : SearchBackgroundArtworkButton;
        DirectionalFocusNavigator.Focus(button);
    }

    private void FocusMetadataSearchQuery() =>
        Dispatcher.UIThread.Post(() => DirectionalFocusNavigator.Focus(MetadataSearchQueryBox));

    private void FocusMetadataSearchButton() => DirectionalFocusNavigator.Focus(OpenMetadataSearchButton);

    internal async Task<bool> HandleMetadataSearchKeyAsync(KeyEventArgs e, DirectionalFocusNavigator focusNavigator)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.AddGame.IsMetadataSearchOpen)
        {
            return false;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseMetadataSearchFromInput();
            return true;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (IsMetadataSearchQueryFocused || IsMetadataSearchRunButtonFocused)
            {
                await viewModel.AddGame.SearchMetadataAsync();
            }
            else if (IsMetadataSearchResultsFocused)
            {
                DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
            }
            else if (IsMetadataSearchApplyButtonFocused)
            {
                ApplySelectedMetadataFromInput();
            }
            else if (IsMetadataSearchCloseButtonFocused || IsMetadataSearchCancelButtonFocused)
            {
                CloseMetadataSearchFromInput();
            }

            return true;
        }

        if (!DirectionalFocusNavigator.IsArrowKey(e.Key) ||
            DirectionalFocusNavigator.IsTextInput(e.Source, e.Key))
        {
            return true;
        }

        if (MoveMetadataSearchFocus(e.Key))
        {
            e.Handled = true;
            return true;
        }

        if (IsMetadataSearchResultsFocused && (e.Key is Key.Up or Key.Down))
        {
            return true;
        }

        e.Handled = true;
        focusNavigator.MoveFocus(MetadataSearchOverlay, e.Key, useNativeArrowBehavior: false);
        return true;
    }

    private bool MoveMetadataSearchFocus(Key key)
    {
        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var sourceTabsFocused = ReferenceEquals(focusedElement, MetadataSearchSourceTabs);
        if (focusedElement is Control focusedControl)
        {
            var focusedTab = focusedControl as TabItem ?? focusedControl.GetVisualAncestors()
                .OfType<TabItem>()
                .FirstOrDefault();
            if (focusedTab is not null && focusedTab.GetVisualAncestors().Contains(MetadataSearchSourceTabs))
            {
                sourceTabsFocused = true;
            }
        }

        if (sourceTabsFocused && (key is Key.Left or Key.Right))
        {
            var indexOffset = key == Key.Right ? 1 : -1;
            var selectedIndex = MetadataSearchSourceTabs.SelectedIndex + indexOffset;
            if ((uint)selectedIndex < (uint)MetadataSearchSourceTabs.Items.Count)
            {
                MetadataSearchSourceTabs.SelectedIndex = selectedIndex;
                Dispatcher.UIThread.Post(() =>
                {
                    if (MetadataSearchSourceTabs.ContainerFromIndex(selectedIndex) is TabItem selectedTab)
                    {
                        DirectionalFocusNavigator.Focus(selectedTab);
                    }
                });
            }

            return true;
        }

        if (sourceTabsFocused && key == Key.Down)
        {
            FocusSelectedMetadataSearchResult();
            return true;
        }

        var resultsList = GetMetadataSearchResultsList();
        if (IsMetadataSearchResultsFocused && resultsList is not null)
        {
            if (key == Key.Right ||
                key == Key.Down && resultsList.SelectedIndex == resultsList.Items.Count - 1)
            {
                DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
                return true;
            }
        }

        if (ApplyMetadataSearchButton.IsFocused && (key is Key.Left or Key.Up))
        {
            FocusSelectedMetadataSearchResult();
            return true;
        }

        if (CancelMetadataSearchButton.IsFocused && key == Key.Right)
        {
            DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
            return true;
        }

        return false;
    }

    private void ApplySelectedMetadataFromInput()
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.AddGame.CanApplySelectedMetadata)
        {
            return;
        }

        viewModel.AddGame.ApplySelectedMetadata();
        Dispatcher.UIThread.Post(FocusMetadataSearchButton);
    }

    private void CloseMetadataSearchFromInput()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AddGame.CloseMetadataSearch();
        Dispatcher.UIThread.Post(FocusMetadataSearchButton);
    }

    public void FocusSelectedSection()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var section = AddGameSectionList.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.AddGame.SelectedSection));
        section?.FocusItem();
    }

    public void FocusSelectedField()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var fields = GetFocusableEditorFields();
        var fieldIndex = viewModel.AddGame.SelectedFieldIndex;
        if ((uint)fieldIndex < (uint)fields.Count)
        {
            DirectionalFocusNavigator.Focus(fields[fieldIndex]);
        }
    }

    private async void OnChooseExecutableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not Control { DataContext: AddGameActionViewModel action })
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
            viewModel.AddGame.InstallDirectory ??= Path.GetDirectoryName(executablePath);
            viewModel.StatusMessage = null;
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = string.Format(AppResources.SelectExecutableError, exception.Message);
        }
    }

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
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
                viewModel.AddGame.InstallDirectory = folderPath;
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
        if (DataContext is MainWindowViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: AddGameSectionViewModel section })
        {
            viewModel.AddGame.SelectSection(section);
        }
    }

    private async void OnOpenMetadataSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AddGame.OpenMetadataSearch();
        FocusMetadataSearchQuery();
        if (viewModel.AddGame.CanSearchMetadata)
        {
            await viewModel.AddGame.SearchMetadataAsync();
        }
    }

    private void OnCloseMetadataSearchClick(object? sender, RoutedEventArgs e)
    {
        CloseMetadataSearchFromInput();
    }

    private void OnApplySelectedMetadataClick(object? sender, RoutedEventArgs e)
    {
        ApplySelectedMetadataFromInput();
    }

    private void FocusSelectedMetadataSearchResult()
    {
        var resultsList = GetMetadataSearchResultsList();
        if (resultsList is null)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(resultsList);
        Dispatcher.UIThread.Post(() =>
        {
            if (resultsList.SelectedItem is not { } selectedResult)
            {
                return;
            }

            var selectedItem = resultsList.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, selectedResult));
            if (selectedItem is not null)
            {
                DirectionalFocusNavigator.Focus(selectedItem);
            }
        });
    }

    private ListBox? GetMetadataSearchResultsList() => MetadataSearchOverlay.GetVisualDescendants()
        .OfType<ListBox>()
        .FirstOrDefault(listBox => listBox.IsEffectivelyVisible);

    private void OnSectionActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: AddGameSectionViewModel section })
        {
            viewModel.AddGame.SelectSection(section);
            viewModel.AddGame.ActivateContent();
            FocusSelectedField();
        }
    }

    private void OnAddActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AddGame.AddActionCommand.Execute(null);
        var addedAction = viewModel.AddGame.Actions.Last();
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
        if (DataContext is not MainWindowViewModel viewModel || e.Source is not Control focusedControl)
        {
            return;
        }

        var fields = GetFocusableEditorFields();
        var focusedField = fields.IndexOf(focusedControl);
        if (focusedField >= 0)
        {
            viewModel.AddGame.SelectField(focusedField);
        }
    }

    private List<Control> GetFocusableEditorFields()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return [];
        }

        var activePanel = viewModel.AddGame.SelectedSection.Id switch
        {
            AddGameViewModel.GameSectionId => GamePanel,
            AddGameViewModel.ActionsSectionId => ActionsPanel,
            AddGameViewModel.ImagesSectionId => ImagesPanel,
            _ => throw new InvalidOperationException($"Unknown add-game section '{viewModel.AddGame.SelectedSection.Id}'.")
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
        if (DataContext is not MainWindowViewModel viewModel || sender is not Button { Tag: string slotName })
        {
            return;
        }

        var slot = slotName switch
        {
            "Cover" => GameArtworkSlot.Cover,
            "Background" => GameArtworkSlot.Background,
            _ => throw new ArgumentOutOfRangeException(nameof(slotName), slotName, "Unknown artwork slot.")
        };
        await viewModel.AddGame.SearchArtworkAsync(slot);
        FocusSelectedArtworkOption();
    }

    private void OnUseSelectedArtworkClick(object? sender, RoutedEventArgs e)
    {
        UseFocusedArtworkOption();
    }

    public void UseFocusedArtworkOption()
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.AddGame.ArtworkPicker.CanApplyActiveArtwork)
        {
            return;
        }

        viewModel.AddGame.ApplySelectedArtwork();
        FocusActiveArtworkSearchButton();
    }

    private void OnCloseArtworkPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var slot = viewModel.AddGame.ArtworkPicker.ActiveArtworkSlot;
        viewModel.AddGame.CloseArtworkSearch();
        if (slot == GameArtworkSlot.Cover)
        {
            DirectionalFocusNavigator.Focus(SearchCoverArtworkButton);
        }
        else
        {
            DirectionalFocusNavigator.Focus(SearchBackgroundArtworkButton);
        }
    }

    private ListBox? GetActiveArtworkList()
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.AddGame.ArtworkPicker.IsArtworkPickerOpen)
        {
            return null;
        }

        return viewModel.AddGame.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? CoverArtworkOptions
            : BackgroundArtworkOptions;
    }
}
