using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views;
using Hatband.App.Views.Components;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Modals;

/// <summary>Owns search-dialog focus, provider-tab switching, and Enter/Back behavior.</summary>
public partial class MetadataSearchModalView : ModalView
{
    public MetadataSearchModalView()
    {
        InitializeComponent();
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

        if (action == NavigationAction.Confirm)
        {
            HandleConfirm(originalEvent);
            return NavigationActionHandling.Handled;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            if (DirectionalFocusNavigator.IsTextInput(originalEvent.Source, originalEvent.Key))
            {
                return NavigationActionHandling.Native;
            }

            if (MoveModalFocus(action))
            {
                originalEvent.Handled = true;
                return NavigationActionHandling.Handled;
            }

            if (GetActiveResults() is { } resultList && IsFocusedWithin(resultList) &&
                (action is NavigationAction.Up or NavigationAction.Down))
            {
                return NavigationActionHandling.Native;
            }

            originalEvent.Handled = true;
            MoveFocusWithinModal(originalEvent.Key);
            return NavigationActionHandling.Handled;
        }

        return base.HandleNavigationAction(action, originalEvent);
    }

    public override bool TryHandleBack(KeyEventArgs originalEvent)
    {
        originalEvent.Handled = true;
        if (DataContext is MetadataSearchModalViewModel viewModel)
        {
            viewModel.CancelSearchCommand.Execute(null);
        }

        return true;
    }

    protected override Control? GetInitialFocusTarget() => MetadataSearchQueryBox;

    private void HandleConfirm(KeyEventArgs originalEvent)
    {
        originalEvent.Handled = true;
        if (DataContext is not MetadataSearchModalViewModel viewModel)
        {
            return;
        }

        if (MetadataSearchQueryBox.IsFocused || MetadataSearchRunButton.IsFocused)
        {
            viewModel.SearchCommand.Execute(null);
            return;
        }

        if (GetActiveResults() is { } resultsList &&
            (resultsList.IsFocused || resultsList.GetVisualDescendants().OfType<Control>().Any(control => control.IsFocused)))
        {
            DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
            return;
        }

        if (ApplyMetadataSearchButton.IsFocused)
        {
            viewModel.ApplySelectedResultCommand.Execute(null);
            return;
        }

        if (CloseMetadataSearchButton.IsFocused || CancelMetadataSearchButton.IsFocused)
        {
            viewModel.CancelSearchCommand.Execute(null);
        }
    }

    private bool MoveModalFocus(NavigationAction action)
    {
        var sourceTabsFocused = IsSourceTabFocused();
        if (sourceTabsFocused && (action is NavigationAction.Left or NavigationAction.Right))
        {
            var indexOffset = action == NavigationAction.Right ? 1 : -1;
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

        if (sourceTabsFocused && action == NavigationAction.Down)
        {
            FocusSelectedMetadataSearchResult();
            return true;
        }

        var resultsList = GetActiveResults();
        if (resultsList is not null && IsFocusedWithin(resultsList))
        {
            if (action == NavigationAction.Right ||
                action == NavigationAction.Down && resultsList.SelectedIndex == resultsList.Items.Count - 1)
            {
                DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
                return true;
            }
        }

        if (ApplyMetadataSearchButton.IsFocused && (action is NavigationAction.Left or NavigationAction.Up))
        {
            FocusSelectedMetadataSearchResult();
            return true;
        }

        if (CancelMetadataSearchButton.IsFocused && action == NavigationAction.Right)
        {
            DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
            return true;
        }

        return false;
    }

    private bool IsSourceTabFocused()
    {
        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (ReferenceEquals(focusedElement, MetadataSearchSourceTabs))
        {
            return true;
        }

        if (focusedElement is not Control focusedControl)
        {
            return false;
        }

        var focusedTab = focusedControl as TabItem ?? focusedControl.GetVisualAncestors()
            .OfType<TabItem>()
            .FirstOrDefault();
        return focusedTab is not null && focusedTab.GetVisualAncestors().Contains(MetadataSearchSourceTabs);
    }

    private void MoveFocusWithinModal(Key key)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            new DirectionalFocusNavigator(window).MoveFocus(this, key, useNativeArrowBehavior: false);
        }
    }

    private void FocusSelectedMetadataSearchResult()
    {
        var resultsList = GetActiveResults();
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

    private ListBox? GetActiveResults()
    {
        var selectedSource = (DataContext as MetadataSearchModalViewModel)?.SelectedSource;
        return selectedSource is null
            ? null
            : this.GetVisualDescendants()
                .OfType<ListBox>()
                .FirstOrDefault(list => ReferenceEquals(list.DataContext, selectedSource) && list.IsEffectivelyVisible);
    }

    private bool IsFocusedWithin(Control control) =>
        ReferenceEquals(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement(), control) ||
        control.GetVisualDescendants().OfType<Control>().Any(item => item.IsFocused);
}
