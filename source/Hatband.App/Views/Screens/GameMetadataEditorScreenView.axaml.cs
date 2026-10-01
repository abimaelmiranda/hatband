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
        sectionItems[selectedSectionIndex].FocusItem();
    }

    public void MoveEditorSectionSelection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        selectedSectionIndex = Math.Clamp(selectedSectionIndex + Math.Sign(direction), 0, sectionItems.Length - 1);
        UpdateSectionSelection();
        FocusSelectedEditorSection();
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
        target?.Focus();
    }

    public void SearchSelectedMetadataSource()
    {
        if (selectedSectionIndex == 2 && DataContext is MainWindowViewModel viewModel)
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

        activeList.Focus();
        Dispatcher.UIThread.Post(() =>
        {
            if (activeList.SelectedItem is not GameArtworkSourceOption selectedOption)
            {
                return;
            }

            var selectedItem = activeList.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, selectedOption));
            selectedItem?.Focus();
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

    private void OnArtworkSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(1);
    }

    private async void OnSourcesSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(2);
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.GameMetadataEditor.SearchMetadataSourcesAsync();
        }
    }

    private async void OnMetadataSourceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (selectedSectionIndex != 2 || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.GameMetadataEditor.SearchMetadataSourcesAsync();
    }

    private void ActivateSection(int sectionIndex)
    {
        selectedSectionIndex = sectionIndex;
        UpdateSectionSelection();
        FocusEditorSectionContent();
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

        MetadataSectionContent.IsVisible = selectedSectionIndex == 0;
        ArtworkSectionContent.IsVisible = selectedSectionIndex == 1;
        SourcesSectionContent.IsVisible = selectedSectionIndex == 2;
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
        button.Focus();
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
