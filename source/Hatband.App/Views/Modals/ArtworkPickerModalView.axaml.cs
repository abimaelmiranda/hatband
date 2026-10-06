using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels;
using Hatband.App.Views;
using Hatband.App.Views.Navigation;
using Hatband.Core.Enums.Artwork;

namespace Hatband.App.Views.Modals;

/// <summary>Hosts reusable artwork selection and routes focus between previews and modal actions.</summary>
public partial class ArtworkPickerModalView : ModalView
{
    public ArtworkPickerModalView()
    {
        InitializeComponent();
    }

    protected override void OnViewActivated()
    {
        base.OnViewActivated();
        if (DataContext is ArtworkPickerModalViewModel viewModel)
        {
            viewModel.ArtworkPicker.PropertyChanged += OnArtworkPickerPropertyChanged;
        }
    }

    protected override void OnViewDeactivated()
    {
        if (DataContext is ArtworkPickerModalViewModel viewModel)
        {
            viewModel.ArtworkPicker.PropertyChanged -= OnArtworkPickerPropertyChanged;
        }

        base.OnViewDeactivated();
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

        if (action == NavigationAction.Confirm)
        {
            if (DataContext is not ArtworkPickerModalViewModel viewModel)
            {
                return NavigationActionHandling.Native;
            }

            if (ApplyArtworkButton.IsFocused)
            {
                return NavigationCommandExecutor.TryExecute(
                    ApplyArtworkButton,
                    viewModel.ApplySelectedArtworkCommand)
                    ? NavigationActionHandling.Handled
                    : NavigationActionHandling.Native;
            }

            if (CloseArtworkPickerButton.IsFocused)
            {
                return NavigationCommandExecutor.TryExecute(
                    CloseArtworkPickerButton,
                    viewModel.ClosePickerCommand)
                    ? NavigationActionHandling.Handled
                    : NavigationActionHandling.Native;
            }

            if (CancelArtworkPickerButton.IsFocused)
            {
                return NavigationCommandExecutor.TryExecute(
                    CancelArtworkPickerButton,
                    viewModel.ClosePickerCommand)
                    ? NavigationActionHandling.Handled
                    : NavigationActionHandling.Native;
            }

            if (GetActiveArtworkList() is { } list && IsFocusedWithin(list))
            {
                return list.IsEffectivelyEnabled && NavigationCommandExecutor.TryExecute(
                    ApplyArtworkButton,
                    viewModel.ApplySelectedArtworkCommand)
                    ? NavigationActionHandling.Handled
                    : NavigationActionHandling.Native;
            }

            return NavigationActionHandling.Native;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            var activeList = GetActiveArtworkList();
            if (activeList is not null && IsFocusedWithin(activeList))
            {
                if (action == NavigationAction.Right &&
                    activeList.SelectedIndex == activeList.Items.Count - 1)
                {
                    DirectionalFocusNavigator.Focus(ApplyArtworkButton);
                    return NavigationActionHandling.Handled;
                }

                return NavigationActionHandling.Native;
            }

            if (ApplyArtworkButton.IsFocused && (action is NavigationAction.Left or NavigationAction.Up))
            {
                FocusSelectedArtworkOption();
                return NavigationActionHandling.Handled;
            }

            if (CancelArtworkPickerButton.IsFocused && action == NavigationAction.Right)
            {
                DirectionalFocusNavigator.Focus(ApplyArtworkButton);
                return NavigationActionHandling.Handled;
            }

            var direction = GetDirection(action);
            return MoveFocusWithinModal(direction, context)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        return base.HandleNavigationAction(action, context);
    }

    public override bool TryHandleBack()
    {
        if (DataContext is ArtworkPickerModalViewModel viewModel)
        {
            return NavigationCommandExecutor.TryExecute(this, viewModel.ClosePickerCommand);
        }

        return false;
    }

    protected override Control? GetInitialFocusTarget()
    {
        var activeList = GetActiveArtworkList();
        if (activeList is { IsVisible: true })
        {
            return activeList;
        }

        return CancelArtworkPickerButton;
    }

    private void FocusSelectedArtworkOption()
    {
        var activeList = GetActiveArtworkList();
        if (activeList is null)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(activeList);
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
    }

    private void OnArtworkPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameArtworkPickerViewModel.IsCoverPickerActive) or
            nameof(GameArtworkPickerViewModel.IsBackgroundPickerActive))
        {
            Dispatcher.UIThread.Post(FocusSelectedArtworkOption, DispatcherPriority.Background);
        }
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

    private void OnArtworkOptionGotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ArtworkPickerModalViewModel viewModel &&
            e.Source is ListBoxItem { DataContext: GameArtworkSourceOption option })
        {
            viewModel.ArtworkPicker.SelectedActiveArtworkOption = option;
        }
    }

    private ListBox? GetActiveArtworkList()
    {
        if (DataContext is not ArtworkPickerModalViewModel viewModel)
        {
            return null;
        }

        return viewModel.ArtworkPicker.ActiveArtworkSlot == GameArtworkSlot.Cover
            ? CoverArtworkList
            : BackgroundArtworkList;
    }

    private bool IsFocusedWithin(Control control) =>
        control.IsFocused || control.GetVisualDescendants().OfType<Control>().Any(item => item.IsFocused);
}
