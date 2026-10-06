using Avalonia.Controls;
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

    public override bool SupportsTabNavigation =>
        isSectionContentActive && selectedSectionIndex == 2 && MetadataSourceTabs.Items.Count > 1;

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
        ScheduleFocusEditorSectionContent();
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
        NavigationInputContext context)
    {
        if (action == NavigationAction.Back)
        {
            return TryHandleBack()
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        var focusedControl = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var sourceTabFocused = focusedControl is TabItem tab &&
            tab.GetVisualAncestors().Contains(MetadataSourceTabs);
        if (SupportsTabNavigation &&
            (action is NavigationAction.PreviousTab or NavigationAction.NextTab ||
             sourceTabFocused && action is NavigationAction.Left or NavigationAction.Right))
        {
            var offset = action is NavigationAction.NextTab or NavigationAction.Right ? 1 : -1;
            var index = MetadataSourceTabs.SelectedIndex + offset;
            if ((uint)index >= (uint)MetadataSourceTabs.Items.Count)
            {
                return NavigationActionHandling.Handled;
            }

            MetadataSourceTabs.SetCurrentValue(TabControl.SelectedIndexProperty, index);
            Dispatcher.UIThread.Post(() =>
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
                if (IsLoaded && IsEffectivelyVisible && SupportsTabNavigation &&
                    MetadataSourceTabs.SelectedIndex == index && focused is not null &&
                    focused.GetVisualAncestors().Contains(this) &&
                    MetadataSourceTabs.ContainerFromIndex(index) is TabItem selected)
                {
                    DirectionalFocusNavigator.Focus(selected);
                }
            }, DispatcherPriority.Loaded);
            return NavigationActionHandling.Handled;
        }

        if ((action is NavigationAction.Confirm or NavigationAction.Right) && IsEditorSectionFocused)
        {
            ActivateSectionContent();
            if (action == NavigationAction.Confirm && selectedSectionIndex == 2)
            {
                SearchSelectedMetadataSource();
            }

            return NavigationActionHandling.Handled;
        }

        return base.HandleNavigationAction(action, context);
    }

    public override bool TryHandleBack()
    {
        if (DataContext is not GameMetadataEditorViewModel viewModel)
        {
            return false;
        }

        if (isSectionContentActive)
        {
            FocusSelectedEditorSection();
            return true;
        }

        return NavigationCommandExecutor.TryExecute(this, viewModel.CancelCommand);
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
            NavigationCommandExecutor.TryExecute(sender as Control ?? this, viewModel.CancelCommand);
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
        if (!ReferenceEquals(sender, MetadataSourceTabs) || !ReferenceEquals(e.Source, MetadataSourceTabs) ||
            !isSectionContentActive || selectedSectionIndex != 2 ||
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
        ScheduleFocusEditorSectionContent();
    }

    private void ScheduleFocusEditorSectionContent()
    {
        if (DataContext is not GameMetadataEditorViewModel viewModel)
        {
            return;
        }

        var expectedSectionIndex = selectedSectionIndex;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsLoaded || !IsEffectivelyVisible ||
                !ReferenceEquals(DataContext, viewModel) ||
                !isSectionContentActive ||
                selectedSectionIndex != expectedSectionIndex)
            {
                return;
            }

            FocusEditorSectionContent();
        }, DispatcherPriority.Loaded);
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
