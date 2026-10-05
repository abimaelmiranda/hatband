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
            originalEvent.Handled = true;
            if (DataContext is ArtworkPickerModalViewModel viewModel)
            {
                if (ApplyArtworkButton.IsFocused)
                {
                    viewModel.ApplySelectedArtworkCommand.Execute(null);
                }
                else if (CloseArtworkPickerButton.IsFocused || CancelArtworkPickerButton.IsFocused)
                {
                    viewModel.ClosePickerCommand.Execute(null);
                }
                else if (GetActiveArtworkList() is { } list && IsFocusedWithin(list))
                {
                    viewModel.ApplySelectedArtworkCommand.Execute(null);
                }
            }

            return NavigationActionHandling.Handled;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            var activeList = GetActiveArtworkList();
            if (activeList is not null && IsFocusedWithin(activeList))
            {
                if (action == NavigationAction.Right &&
                    activeList.SelectedIndex == activeList.Items.Count - 1)
                {
                    originalEvent.Handled = true;
                    DirectionalFocusNavigator.Focus(ApplyArtworkButton);
                    return NavigationActionHandling.Handled;
                }

                return NavigationActionHandling.Native;
            }

            if (ApplyArtworkButton.IsFocused && (action is NavigationAction.Left or NavigationAction.Up))
            {
                FocusSelectedArtworkOption();
                originalEvent.Handled = true;
                return NavigationActionHandling.Handled;
            }

            if (CancelArtworkPickerButton.IsFocused && action == NavigationAction.Right)
            {
                originalEvent.Handled = true;
                DirectionalFocusNavigator.Focus(ApplyArtworkButton);
                return NavigationActionHandling.Handled;
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
        if (DataContext is ArtworkPickerModalViewModel viewModel)
        {
            viewModel.ClosePickerCommand.Execute(null);
        }

        return true;
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

    private void MoveFocusWithinModal(Key key)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            new DirectionalFocusNavigator(window).MoveFocus(this, key, useNativeArrowBehavior: false);
        }
    }

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
