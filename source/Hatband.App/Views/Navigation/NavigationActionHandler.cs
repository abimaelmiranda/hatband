using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Hatband.App.Navigation;

namespace Hatband.App.Views.Navigation;

internal static class NavigationActionHandler
{
    public static NavigationActionHandling Handle(Control root, NavigationAction action, NavigationInputContext context)
    {
        if (action == NavigationAction.Confirm)
        {
            return NavigationActionHandling.Native;
        }

        if (action is NavigationAction.Up or NavigationAction.Down or NavigationAction.Left or NavigationAction.Right)
        {
            var focusedControl = TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() as Control;
            var direction = GetDirection(action);
            if (context.Source == InputSource.Keyboard && focusedControl is TextBox textBox &&
                DirectionalFocusNavigator.IsTextInput(textBox, direction))
            {
                return NavigationActionHandling.Native;
            }

            if (UsesNativeArrowInput(focusedControl, action))
            {
                return NavigationActionHandling.Native;
            }

            var window = TopLevel.GetTopLevel(root) as Window;
            if (window is null)
            {
                return NavigationActionHandling.Unhandled;
            }

            var navigator = new DirectionalFocusNavigator(window);
            return navigator.MoveFocus(root, direction, context.Source == InputSource.Keyboard)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        return NavigationActionHandling.Unhandled;
    }

    private static NavigationDirection GetDirection(NavigationAction action) => action switch
    {
        NavigationAction.Up => NavigationDirection.Up,
        NavigationAction.Down => NavigationDirection.Down,
        NavigationAction.Left => NavigationDirection.Left,
        NavigationAction.Right => NavigationDirection.Right,
        _ => throw new InvalidOperationException("Unsupported directional action.")
    };

    private static bool UsesNativeArrowInput(Control? control, NavigationAction action)
    {
        if (control is null)
        {
            return false;
        }

        if (control is ComboBox { IsDropDownOpen: true } ||
            control.GetVisualAncestors().OfType<ComboBox>().Any(comboBox => comboBox.IsDropDownOpen))
        {
            return true;
        }

        var isListInput = control is ListBox || control.GetVisualAncestors().OfType<ListBox>().Any();
        return isListInput && action is NavigationAction.Up or NavigationAction.Down;
    }
}
