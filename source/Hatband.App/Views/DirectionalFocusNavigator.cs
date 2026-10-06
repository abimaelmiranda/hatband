using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Hatband.App.Views.Components;

namespace Hatband.App.Views;

internal sealed class DirectionalFocusNavigator
{
    private readonly Window _window;

    public DirectionalFocusNavigator(Window window)
    {
        _window = window;
    }

    public bool MoveFocus(Control navigationRoot, Key key, bool useNativeArrowBehavior = true)
    {
        var focusedControl = navigationRoot.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.IsFocused);
        if (focusedControl is not null && useNativeArrowBehavior && IsNativeArrowControl(focusedControl))
        {
            return false;
        }

        var direction = GetArrowDirection(key);
        if (direction is null)
        {
            return false;
        }

        var focusManager = TopLevel.GetTopLevel(_window)?.FocusManager;
        if (focusManager is null)
        {
            return false;
        }

        if (focusedControl is null)
        {
            return FocusFirstInDirection(navigationRoot, direction.Value == NavigationDirection.Up);
        }

        var resolvedNavigationRoot = ResolveNavigationScope(navigationRoot, focusedControl);
        var isInsideNavigationScope = !ReferenceEquals(navigationRoot, resolvedNavigationRoot);
        navigationRoot = resolvedNavigationRoot;

        var nextElement = focusManager.FindNextElement(
            direction.Value,
            new FindNextElementOptions
            {
                FocusedElement = focusedControl,
                SearchRoot = navigationRoot,
                NavigationStrategyOverride = XYFocusNavigationStrategy.Projection
            });

        if (nextElement is Control nextControl &&
            !ReferenceEquals(nextControl, focusedControl) &&
            IsInDirection(focusedControl, nextControl, navigationRoot, direction.Value) &&
            focusManager.Focus(nextElement, NavigationMethod.Directional, KeyModifiers.None))
        {
            var focusedAfterMove = navigationRoot.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => control.IsFocused);
            if (focusedAfterMove is not null && !ReferenceEquals(focusedAfterMove, focusedControl))
            {
                return true;
            }
        }

        var directionalNeighbor = FindDirectionalNeighbor(navigationRoot, focusedControl, direction.Value);
        if (directionalNeighbor is not null && Focus(directionalNeighbor))
        {
            return true;
        }

        return isInsideNavigationScope;
    }

    public static bool Focus(Control control) => control.Focus(NavigationMethod.Directional);

    public static bool IsTextInput(object? source, Key key)
    {
        if (source is not TextBox textBox)
        {
            return false;
        }

        if (key is Key.Enter or Key.Left or Key.Right)
        {
            return true;
        }

        return textBox.AcceptsReturn && key is Key.Up or Key.Down;
    }

    public static bool IsArrowKey(Key key) => key is Key.Up or Key.Down or Key.Left or Key.Right;

    private static Control ResolveNavigationScope(Control navigationRoot, Control focusedControl)
    {
        var layout = focusedControl.GetVisualAncestors()
            .OfType<FullScreenNavigationLayout>()
            .FirstOrDefault();
        if (layout is null)
        {
            return navigationRoot;
        }

        if (layout.NavigationContentRoot is { } navigationContentRoot &&
            IsWithin(focusedControl, navigationContentRoot))
        {
            return navigationContentRoot;
        }

        if (layout.MainContentRoot is { } mainContentRoot && IsWithin(focusedControl, mainContentRoot))
        {
            return layout.FooterContentRoot is null ? mainContentRoot : layout;
        }

        if (layout.FooterContentRoot is { } footerContentRoot && IsWithin(focusedControl, footerContentRoot))
        {
            return layout;
        }

        return navigationRoot;
    }

    private static bool IsWithin(Control control, Control? root)
    {
        return root is not null &&
               (ReferenceEquals(control, root) || control.GetVisualAncestors().Contains(root));
    }

    private static bool IsNativeArrowControl(Control control)
    {
        if (control is ComboBox comboBox)
        {
            return comboBox.IsDropDownOpen;
        }

        var parentComboBox = control.GetVisualAncestors().OfType<ComboBox>().FirstOrDefault();
        return parentComboBox?.IsDropDownOpen == true;
    }

    private static bool FocusFirstInDirection(Control navigationRoot, bool reverse)
    {
        var candidates = GetFocusableControls(navigationRoot);
        if (candidates.Count == 0)
        {
            return false;
        }

        return Focus(reverse ? candidates[^1] : candidates[0]);
    }

    private static Control? FindDirectionalNeighbor(
        Control navigationRoot,
        Control focusedControl,
        NavigationDirection direction)
    {
        var origin = GetCenter(focusedControl, navigationRoot);
        if (origin is null)
        {
            return null;
        }

        Control? closestControl = null;
        var closestScore = double.PositiveInfinity;
        foreach (var candidate in GetFocusableControls(navigationRoot))
        {
            if (ReferenceEquals(candidate, focusedControl))
            {
                continue;
            }

            var candidateCenter = GetCenter(candidate, navigationRoot);
            if (candidateCenter is null)
            {
                continue;
            }

            var horizontalDistance = candidateCenter.Value.X - origin.Value.X;
            var verticalDistance = candidateCenter.Value.Y - origin.Value.Y;
            var (primaryDistance, secondaryDistance) = direction switch
            {
                NavigationDirection.Up when verticalDistance < 0 => (-verticalDistance, Math.Abs(horizontalDistance)),
                NavigationDirection.Down when verticalDistance > 0 => (verticalDistance, Math.Abs(horizontalDistance)),
                NavigationDirection.Left when horizontalDistance < 0 => (-horizontalDistance, Math.Abs(verticalDistance)),
                NavigationDirection.Right when horizontalDistance > 0 => (horizontalDistance, Math.Abs(verticalDistance)),
                _ => (double.PositiveInfinity, double.PositiveInfinity)
            };
            var score = primaryDistance + secondaryDistance * 1.5;
            if (score < closestScore)
            {
                closestScore = score;
                closestControl = candidate;
            }
        }

        return closestControl;
    }

    private static Point? GetCenter(Control control, Control relativeTo)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), relativeTo);
        return topLeft is null
            ? null
            : new Point(topLeft.Value.X + control.Bounds.Width / 2, topLeft.Value.Y + control.Bounds.Height / 2);
    }

    private static bool IsInDirection(
        Control originControl,
        Control candidateControl,
        Control relativeTo,
        NavigationDirection direction)
    {
        var origin = GetCenter(originControl, relativeTo);
        var candidate = GetCenter(candidateControl, relativeTo);
        if (origin is null || candidate is null)
        {
            return false;
        }

        return direction switch
        {
            NavigationDirection.Up => candidate.Value.Y < origin.Value.Y,
            NavigationDirection.Down => candidate.Value.Y > origin.Value.Y,
            NavigationDirection.Left => candidate.Value.X < origin.Value.X,
            NavigationDirection.Right => candidate.Value.X > origin.Value.X,
            _ => false
        };
    }

    private static List<Control> GetFocusableControls(Control navigationRoot)
    {
        return navigationRoot.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Focusable &&
                              control.IsTabStop &&
                              control.IsEffectivelyVisible &&
                              control.IsEffectivelyEnabled)
            .ToList();
    }

    private static NavigationDirection? GetArrowDirection(Key key) => key switch
    {
        Key.Up => NavigationDirection.Up,
        Key.Down => NavigationDirection.Down,
        Key.Left => NavigationDirection.Left,
        Key.Right => NavigationDirection.Right,
        _ => null
    };

}
