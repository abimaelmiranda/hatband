using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.Views.Components;

namespace Hatband.App.Views.Navigation;

internal static class FocusedControlNavigationAdapter
{
    public static bool TryHandleOpenComboBox(Control root, NavigationAction action)
    {
        var comboBox = root.GetVisualDescendants().OfType<ComboBox>()
            .FirstOrDefault(control => control.IsDropDownOpen && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
        if (comboBox is null)
        {
            return false;
        }

        switch (action)
        {
            case NavigationAction.Back:
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
                DirectionalFocusNavigator.Focus(comboBox);
                return true;
            case NavigationAction.Confirm:
                if (comboBox is SettingsComboBox settingsComboBox)
                {
                    settingsComboBox.ConfirmSelection();
                }
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
                DirectionalFocusNavigator.Focus(comboBox);
                return true;
            case NavigationAction.Up:
            case NavigationAction.Down:
                MoveSelection(comboBox, action == NavigationAction.Down ? 1 : -1);
                return true;
            default:
                return false;
        }
    }

    public static bool TryHandle(Control root, NavigationAction action)
    {
        var focused = TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() as Control;
        if (focused is null || !focused.IsEffectivelyEnabled ||
            !(ReferenceEquals(focused, root) || focused.GetVisualAncestors().Contains(root)))
        {
            return false;
        }

        var control = focused;
        if (action == NavigationAction.Confirm)
        {
            if (FindAncestorOrSelf<ComboBox>(control) is { } comboBox)
            {
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
                return true;
            }

            var actionable = FindAncestorOrSelf<ToggleButton>(control) as Control
                ?? FindAncestorOrSelf<Button>(control);
            if (actionable is null || !actionable.IsEffectivelyEnabled)
            {
                return false;
            }

            var peer = ControlAutomationPeer.CreatePeerForElement(actionable);
            if (peer is IToggleProvider toggle)
            {
                toggle.Toggle();
                return true;
            }

            if (peer is IInvokeProvider invoke)
            {
                invoke.Invoke();
                return true;
            }

            return false;
        }

        if (FindAncestorOrSelf<ListBox>(control) is { } list &&
            action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            MoveSelection(list, action is NavigationAction.Down or NavigationAction.Right ? 1 : -1);
            return true;
        }

        return false;
    }

    private static T? FindAncestorOrSelf<T>(Control control) where T : Control
    {
        return control as T ?? control.GetVisualAncestors().OfType<T>().FirstOrDefault();
    }

    private static void MoveSelection(SelectingItemsControl control, int offset)
    {
        if (control.Items.Count == 0)
        {
            return;
        }

        var index = control.SelectedIndex < 0 ? 0 : Math.Clamp(control.SelectedIndex + offset, 0, control.Items.Count - 1);
        control.SetCurrentValue(SelectingItemsControl.SelectedIndexProperty, index);
        if (control is ListBox list)
        {
            list.ScrollIntoView(index);
            Dispatcher.UIThread.Post(() =>
            {
                var focused = TopLevel.GetTopLevel(list)?.FocusManager?.GetFocusedElement() as Control;
                if (list.SelectedIndex == index && focused is not null &&
                    (ReferenceEquals(focused, list) || focused.GetVisualAncestors().Contains(list)) &&
                    list.ContainerFromIndex(index) is Control { IsEffectivelyEnabled: true } selected)
                {
                    DirectionalFocusNavigator.Focus(selected);
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
