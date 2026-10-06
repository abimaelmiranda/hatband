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

    public override bool SupportsTabNavigation => true;

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

        if (action is NavigationAction.PreviousTab or NavigationAction.NextTab)
        {
            return MoveSelectedTab(action == NavigationAction.NextTab ? 1 : -1)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        if (action == NavigationAction.Confirm)
        {
            return HandleConfirm()
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Native;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            var focusedControl = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            var direction = GetDirection(action);
            if (context.Source == InputSource.Keyboard && focusedControl is TextBox textBox &&
                DirectionalFocusNavigator.IsTextInput(textBox, direction))
            {
                return NavigationActionHandling.Native;
            }

            if (MoveModalFocus(action))
            {
                return NavigationActionHandling.Handled;
            }

            if (GetActiveResults() is { } resultList && IsFocusedWithin(resultList) &&
                (action is NavigationAction.Up or NavigationAction.Down))
            {
                return NavigationActionHandling.Native;
            }

            return MoveFocusWithinModal(direction, context)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        return base.HandleNavigationAction(action, context);
    }

    public override bool TryHandleBack()
    {
        if (DataContext is MetadataSearchModalViewModel viewModel)
        {
            return NavigationCommandExecutor.TryExecute(this, viewModel.CancelSearchCommand);
        }

        return false;
    }

    protected override Control? GetInitialFocusTarget() => MetadataSearchQueryBox;

    private bool HandleConfirm()
    {
        if (DataContext is not MetadataSearchModalViewModel viewModel)
        {
            return false;
        }

        if (MetadataSearchQueryBox.IsFocused)
        {
            return viewModel.CanSearch &&
                NavigationCommandExecutor.TryExecute(MetadataSearchQueryBox, viewModel.SearchCommand);
        }

        if (MetadataSearchRunButton.IsFocused)
        {
            return NavigationCommandExecutor.TryExecute(MetadataSearchRunButton, viewModel.SearchCommand);
        }

        if (GetActiveResults() is { } resultsList &&
            (resultsList.IsFocused || resultsList.GetVisualDescendants().OfType<Control>().Any(control => control.IsFocused)))
        {
            if (!ApplyMetadataSearchButton.IsEffectivelyEnabled ||
                !viewModel.ApplySelectedResultCommand.CanExecute(null))
            {
                return false;
            }

            DirectionalFocusNavigator.Focus(ApplyMetadataSearchButton);
            return true;
        }

        if (ApplyMetadataSearchButton.IsFocused)
        {
            return NavigationCommandExecutor.TryExecute(
                ApplyMetadataSearchButton,
                viewModel.ApplySelectedResultCommand);
        }

        if (CloseMetadataSearchButton.IsFocused)
        {
            return NavigationCommandExecutor.TryExecute(CloseMetadataSearchButton, viewModel.CancelSearchCommand);
        }

        if (CancelMetadataSearchButton.IsFocused)
        {
            return NavigationCommandExecutor.TryExecute(CancelMetadataSearchButton, viewModel.CancelSearchCommand);
        }

        return false;
    }

    private bool MoveSelectedTab(int offset)
    {
        var selectedIndex = MetadataSearchSourceTabs.SelectedIndex + offset;
        if ((uint)selectedIndex >= (uint)MetadataSearchSourceTabs.Items.Count)
        {
            return false;
        }

        MetadataSearchSourceTabs.SelectedIndex = selectedIndex;
        Dispatcher.UIThread.Post(() =>
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            if (IsLoaded && IsEffectivelyVisible && MetadataSearchSourceTabs.SelectedIndex == selectedIndex &&
                focused is not null && focused.GetVisualAncestors().Contains(this) &&
                MetadataSearchSourceTabs.ContainerFromIndex(selectedIndex) is TabItem selectedTab)
            {
                DirectionalFocusNavigator.Focus(selectedTab);
            }
        }, DispatcherPriority.Loaded);
        return true;
    }

    private bool MoveModalFocus(NavigationAction action)
    {
        var sourceTabsFocused = IsSourceTabFocused();
        if (sourceTabsFocused && (action is NavigationAction.Left or NavigationAction.Right))
        {
            MoveSelectedTab(action == NavigationAction.Right ? 1 : -1);

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

    private bool MoveFocusWithinModal(NavigationDirection direction, NavigationInputContext context)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            return new DirectionalFocusNavigator(window).MoveFocus(
                this,
                direction,
                context.Source == InputSource.Keyboard);
        }

        return false;
    }

    private static NavigationDirection GetDirection(NavigationAction action) => action switch
    {
        NavigationAction.Up => NavigationDirection.Up,
        NavigationAction.Down => NavigationDirection.Down,
        NavigationAction.Left => NavigationDirection.Left,
        NavigationAction.Right => NavigationDirection.Right,
        _ => throw new InvalidOperationException("Unsupported directional action.")
    };

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
