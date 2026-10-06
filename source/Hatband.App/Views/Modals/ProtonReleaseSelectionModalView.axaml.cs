using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Settings;
using Hatband.App.Views;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Modals;

public partial class ProtonReleaseSelectionModalView : ModalView
{
    public ProtonReleaseSelectionModalView()
    {
        InitializeComponent();
    }

    protected override void OnViewActivated()
    {
        base.OnViewActivated();
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (!viewModel.HasLoadedCatalog && !viewModel.IsLoading)
        {
            viewModel.LoadCatalogCommand.Execute(null);
        }
    }

    protected override void OnViewDeactivated()
    {
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.CancelLoad();
        }

        base.OnViewDeactivated();
    }

    protected override Control? GetInitialFocusTarget()
    {
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (viewModel is null || viewModel.IsLoading)
        {
            return CancelButton;
        }

        if (viewModel.HasError)
        {
            return RetryButton;
        }

        return ReleaseList;
    }

    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, KeyEventArgs originalEvent)
    {
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (action == NavigationAction.Confirm && viewModel is not null)
        {
            originalEvent.Handled = true;
            if (IsFocusedWithin(ReleaseList) || SelectButton.IsFocused)
            {
                viewModel.ConfirmSelectionCommand.Execute(null);
            }
            else if (RetryButton.IsFocused)
            {
                viewModel.LoadCatalogCommand.Execute(null);
            }
            else if (CancelButton.IsFocused)
            {
                viewModel.CancelSelectionCommand.Execute(null);
            }

            return NavigationActionHandling.Handled;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            return base.HandleNavigationAction(action, originalEvent);
        }

        return base.HandleNavigationAction(action, originalEvent);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (e.PropertyName == nameof(ProtonReleaseSelectionModalViewModel.IsLoading) &&
            viewModel is not null &&
            !viewModel.IsLoading)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (viewModel.HasError &&
                    (IsFocusedWithin(CancelButton) || IsFocusedWithin(ReleaseList) || !HasAnyFocus()))
                {
                    DirectionalFocusNavigator.Focus(RetryButton);
                    return;
                }

                if (viewModel.HasReleases &&
                    (IsFocusedWithin(CancelButton) || IsFocusedWithin(RetryButton) || IsFocusedWithin(ReleaseList) || !HasAnyFocus()))
                {
                    FocusSelectedRelease();
                    return;
                }

                if (viewModel.HasNoReleases &&
                    (IsFocusedWithin(CancelButton) || IsFocusedWithin(RetryButton) || IsFocusedWithin(ReleaseList) || !HasAnyFocus()))
                {
                    DirectionalFocusNavigator.Focus(ReleaseList);
                }
            });
        }
    }

    private void FocusSelectedRelease()
    {
        var viewModel = DataContext as ProtonReleaseSelectionModalViewModel;
        if (viewModel is null || viewModel.SelectedRelease is null)
        {
            DirectionalFocusNavigator.Focus(ReleaseList);
            return;
        }

        DirectionalFocusNavigator.Focus(ReleaseList);
        Dispatcher.UIThread.Post(() =>
        {
            var selectedItem = ReleaseList.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.SelectedRelease));
            if (selectedItem is not null)
            {
                DirectionalFocusNavigator.Focus(selectedItem);
            }
        });
    }

    private bool IsFocusedWithin(Control control) =>
        ReferenceEquals(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement(), control) ||
        control.GetVisualDescendants().OfType<Control>().Any(item => item.IsFocused);

    private bool HasAnyFocus() =>
        this.GetVisualDescendants().OfType<Control>().Any(control => control.IsFocused);
}
