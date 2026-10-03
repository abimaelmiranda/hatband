using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.Views.Components;
using Hatband.Core.Enums.Artwork;

namespace Hatband.App.Views.Screens;

public partial class GameMetadataEditorScreenView : UserControl
{
    private readonly ConsoleNavigationItemView[] sectionItems;
    private int selectedSectionIndex;
    private bool isSectionContentActive;

    public GameMetadataEditorScreenView()
    {
        InitializeComponent();
        sectionItems = [MetadataSectionItem, ArtworkSectionItem, SourcesSectionItem];
        UpdateSectionSelection();
    }

    public Control ArtworkPickerNavigationRoot => ArtworkPickerOverlay;

    public bool IsEditorSectionFocused => sectionItems.Any(item => item.HasKeyboardFocus);

    public bool IsArtworkOptionFocused => GetActiveArtworkList() is { } activeList &&
        (activeList.IsFocused || activeList.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Any(item => item.IsFocused));

    public void FocusSelectedEditorSection()
    {
        DeactivateSectionContent();
        sectionItems[selectedSectionIndex].FocusItem();
    }

    public void ActivateSectionContent()
    {
        isSectionContentActive = true;
        NavigationLayout.ActivateMainContent();
        UpdateSectionSelection();
    }

    public void FocusEditorSectionContent()
    {
        var content = GetSelectedSectionContent();
        var target = content.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable &&
                                       control.IsTabStop &&
                                       control.IsEffectivelyVisible &&
                                       control.Bounds.Width > 0 &&
                                       control.Bounds.Height > 0);
        if (target is not null)
        {
            DirectionalFocusNavigator.Focus(target);
        }
    }

    public void SearchSelectedMetadataSource()
    {
        if (isSectionContentActive && selectedSectionIndex == 2 && DataContext is MainWindowViewModel viewModel)
        {
            _ = viewModel.GameMetadataEditor.SearchMetadataSourcesAsync();
        }
    }

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
        }, DispatcherPriority.Background);
    }

    public void FocusActiveArtworkSearchButton()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        FocusArtworkSearchButton(viewModel.GameMetadataEditor.ArtworkPicker.ActiveArtworkSlot);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelGameEditing();
        }
    }

    private void OnMetadataSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(0);
    }

    private void OnMetadataSectionFocusEntered(object? sender, EventArgs e) => SelectSection(0);

    private void OnArtworkSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(1);
    }

    private void OnArtworkSectionFocusEntered(object? sender, EventArgs e) => SelectSection(1);

    private async void OnSourcesSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(2);
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.GameMetadataEditor.SearchMetadataSourcesAsync();
        }
    }

    private void OnSourcesSectionFocusEntered(object? sender, EventArgs e) => SelectSection(2);

    private async void OnMetadataSourceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isSectionContentActive || selectedSectionIndex != 2 || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.GameMetadataEditor.SearchMetadataSourcesAsync();
    }

    private void ActivateSection(int sectionIndex)
    {
        selectedSectionIndex = sectionIndex;
        isSectionContentActive = true;
        NavigationLayout.ActivateMainContent();
        UpdateSectionSelection();
        Dispatcher.UIThread.Post(FocusEditorSectionContent, DispatcherPriority.Background);
    }

    private void SelectSection(int sectionIndex)
    {
        selectedSectionIndex = sectionIndex;
        DeactivateSectionContent();
    }

    private void DeactivateSectionContent()
    {
        isSectionContentActive = false;
        NavigationLayout.DeactivateMainContent();
        UpdateSectionSelection();
    }

    private Control GetSelectedSectionContent()
    {
        return selectedSectionIndex switch
        {
            0 => MetadataSectionContent,
            1 => ArtworkSectionContent,
            2 => SourcesSectionContent,
            _ => throw new ArgumentOutOfRangeException(nameof(selectedSectionIndex))
        };
    }

    private void UpdateSectionSelection()
    {
        for (var index = 0; index < sectionItems.Length; index++)
        {
            sectionItems[index].SetSelected(index == selectedSectionIndex);
        }

        MetadataSectionContent.IsVisible = isSectionContentActive && selectedSectionIndex == 0;
        ArtworkSectionContent.IsVisible = isSectionContentActive && selectedSectionIndex == 1;
        SourcesSectionContent.IsVisible = isSectionContentActive && selectedSectionIndex == 2;
    }

    private async void OnSearchArtworkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is Button { Tag: string slotName })
        {
            await viewModel.GameMetadataEditor.OpenArtworkPickerAsync(ParseArtworkSlot(slotName));
            FocusSelectedArtworkOption();
        }
    }

    private void OnUseSelectedArtworkClick(object? sender, RoutedEventArgs e)
    {
        ApplySelectedArtworkAndRestoreFocus();
    }

    public void UseFocusedArtworkOption()
    {
        ApplySelectedArtworkAndRestoreFocus();
    }

    private void ApplySelectedArtworkAndRestoreFocus()
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.GameMetadataEditor.ArtworkPicker.CanApplyActiveArtwork)
        {
            return;
        }

        var slot = viewModel.GameMetadataEditor.ArtworkPicker.ActiveArtworkSlot;
        viewModel.GameMetadataEditor.ApplySelectedArtwork();
        Dispatcher.UIThread.Post(() => FocusArtworkSearchButton(slot), DispatcherPriority.Background);
    }

    private void OnCloseArtworkPickerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var slot = viewModel.GameMetadataEditor.ArtworkPicker.ActiveArtworkSlot;
            viewModel.GameMetadataEditor.CloseArtworkPicker();
            Dispatcher.UIThread.Post(() => FocusArtworkSearchButton(slot), DispatcherPriority.Background);
        }
    }

    private ListBox? GetActiveArtworkList()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return null;
        }

        return viewModel.GameMetadataEditor.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? CoverArtworkList
            : BackgroundArtworkList;
    }

    private void OnArtworkOptionGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.GameMetadataEditor.ArtworkPicker.IsArtworkPickerOpen &&
            e.Source is ListBoxItem { DataContext: GameArtworkSourceOption option })
        {
            viewModel.GameMetadataEditor.ArtworkPicker.SelectedActiveArtworkOption = option;
        }
    }

    private void FocusArtworkSearchButton(GameArtworkSlot slot)
    {
        var button = slot == GameArtworkSlot.Cover
            ? SearchCoverArtworkButton
            : SearchBackgroundArtworkButton;
        DirectionalFocusNavigator.Focus(button);
    }

    private void OnRestoreArtworkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is Button { Tag: string slotName })
        {
            viewModel.GameMetadataEditor.RestoreArtwork(ParseArtworkSlot(slotName));
        }
    }

    private void OnRestoreMetadataClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is Button { Tag: string fieldName })
        {
            viewModel.GameMetadataEditor.RestoreMetadataFromStore(fieldName);
        }
    }

    private static GameArtworkSlot ParseArtworkSlot(string slotName)
    {
        return slotName switch
        {
            "Cover" => GameArtworkSlot.Cover,
            "Background" => GameArtworkSlot.Background,
            "Icon" => GameArtworkSlot.Icon,
            _ => throw new ArgumentOutOfRangeException(nameof(slotName))
        };
    }
}
