using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views;
using Hatband.App.Views.Components;
using Hatband.App.Views.Navigation;
using Hatband.Core.Enums.Artwork;

namespace Hatband.App.Views.Screens;

/// <summary>Hosts the metadata editor and keeps section, content, and modal navigation scoped locally.</summary>
public partial class GameMetadataEditorScreenView : FullScreenView
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

    public bool IsEditorSectionFocused => sectionItems.Any(item => item.HasKeyboardFocus);

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
        if (isSectionContentActive && selectedSectionIndex == 2 &&
            DataContext is GameMetadataEditorViewModel viewModel)
        {
            _ = viewModel.SearchMetadataSourcesAsync();
        }
    }

    public override NavigationActionHandling HandleNavigationAction(
        NavigationAction action,
        KeyEventArgs originalEvent)
    {
        if (action == NavigationAction.Back)
        {
            return TryHandleBack(originalEvent)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        if ((action is NavigationAction.Confirm or NavigationAction.Right) && IsEditorSectionFocused)
        {
            originalEvent.Handled = true;
            ActivateSectionContent();
            FocusEditorSectionContent();
            if (action == NavigationAction.Confirm && selectedSectionIndex == 2)
            {
                SearchSelectedMetadataSource();
            }

            return NavigationActionHandling.Handled;
        }

        return base.HandleNavigationAction(action, originalEvent);
    }

    public override bool TryHandleBack(KeyEventArgs originalEvent)
    {
        if (DataContext is not GameMetadataEditorViewModel viewModel)
        {
            return false;
        }

        originalEvent.Handled = true;
        if (isSectionContentActive)
        {
            FocusSelectedEditorSection();
            return true;
        }

        viewModel.CancelCommand.Execute(null);
        return true;
    }

    protected override Control? GetInitialFocusTarget()
    {
        if (!isSectionContentActive)
        {
            return sectionItems[selectedSectionIndex].GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault();
        }

        return GetSelectedSectionContent().GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable &&
                                       control.IsTabStop &&
                                       control.IsEffectivelyVisible &&
                                       control.Bounds.Width > 0 &&
                                       control.Bounds.Height > 0);
    }

    protected override void OnViewActivated()
    {
        base.OnViewActivated();
        UpdateSectionSelection();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is GameMetadataEditorViewModel viewModel)
        {
            viewModel.CancelCommand.Execute(null);
        }
    }

    private void OnMetadataSectionActivated(object? sender, EventArgs e) => ActivateSection(0);

    private void OnMetadataSectionFocusEntered(object? sender, EventArgs e) => SelectSection(0);

    private void OnArtworkSectionActivated(object? sender, EventArgs e) => ActivateSection(1);

    private void OnArtworkSectionFocusEntered(object? sender, EventArgs e) => SelectSection(1);

    private async void OnSourcesSectionActivated(object? sender, EventArgs e)
    {
        ActivateSection(2);
        if (DataContext is GameMetadataEditorViewModel viewModel)
        {
            await viewModel.SearchMetadataSourcesAsync();
        }
    }

    private void OnSourcesSectionFocusEntered(object? sender, EventArgs e) => SelectSection(2);

    private async void OnMetadataSourceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isSectionContentActive || selectedSectionIndex != 2 ||
            DataContext is not GameMetadataEditorViewModel viewModel)
        {
            return;
        }

        await viewModel.SearchMetadataSourcesAsync();
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
        if (DataContext is not GameMetadataEditorViewModel viewModel ||
            sender is not Button { Tag: string slotName })
        {
            return;
        }

        e.Handled = true;
        var slot = ParseArtworkSlot(slotName);
        await viewModel.OpenArtworkPickerAsync(slot);
        FocusArtworkSearchButton(slot);
    }

    private void OnRestoreArtworkClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is GameMetadataEditorViewModel viewModel &&
            sender is Button { Tag: string slotName })
        {
            viewModel.RestoreArtwork(ParseArtworkSlot(slotName));
        }
    }

    private void FocusArtworkSearchButton(GameArtworkSlot slot)
    {
        var button = slot == GameArtworkSlot.Cover
            ? SearchCoverArtworkButton
            : SearchBackgroundArtworkButton;
        DirectionalFocusNavigator.Focus(button);
    }

    private static GameArtworkSlot ParseArtworkSlot(string slotName)
    {
        return slotName switch
        {
            "Cover" => GameArtworkSlot.Cover,
            "Background" => GameArtworkSlot.Background,
            "Icon" => GameArtworkSlot.Icon,
            _ => throw new ArgumentOutOfRangeException(nameof(slotName), slotName, "Unknown artwork slot.")
        };
    }
}
