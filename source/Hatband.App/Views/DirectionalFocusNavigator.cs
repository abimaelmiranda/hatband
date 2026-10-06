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

    public bool MoveFocus(Control navigationRoot, NavigationDirection direction, bool useNativeArrowBehavior = true)
    {
        var focusedControl = navigationRoot.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.IsFocused);
        if (focusedControl is not null && useNativeArrowBehavior && IsNativeArrowControl(focusedControl))
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
            return FocusFirstInDirection(navigationRoot, direction == NavigationDirection.Up);
        }

        var resolvedNavigationRoot = ResolveNavigationScope(navigationRoot, focusedControl);
        var isInsideNavigationScope = !ReferenceEquals(navigationRoot, resolvedNavigationRoot);
        navigationRoot = resolvedNavigationRoot;

        var directionalNeighbor = FindDirectionalNeighbor(navigationRoot, focusedControl, direction);
        if (directionalNeighbor is not null && Focus(directionalNeighbor))
        {
            return true;
        }

        var nextElement = focusManager.FindNextElement(
            direction,
            new FindNextElementOptions
            {
                FocusedElement = focusedControl,
                SearchRoot = navigationRoot,
                NavigationStrategyOverride = XYFocusNavigationStrategy.Projection
            });

        if (nextElement is Control nextControl &&
            !ReferenceEquals(nextControl, focusedControl) &&
            IsInDirection(focusedControl, nextControl, navigationRoot, direction) &&
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

        return isInsideNavigationScope;
    }

    public static bool Focus(Control control) => control.Focus(NavigationMethod.Directional);

    public static bool IsTextInput(TextBox textBox, NavigationDirection direction)
    {
        if (direction is NavigationDirection.Left or NavigationDirection.Right)
        {
            return true;
        }

        return textBox.AcceptsReturn && direction is NavigationDirection.Up or NavigationDirection.Down;
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
        var originBounds = GetBounds(focusedControl, navigationRoot);
        if (originBounds is null)
        {
            return null;
        }

        var origin = originBounds.Value.Center;
        Control? closestControl = null;
        var closestScore = double.PositiveInfinity;
        foreach (var candidate in GetFocusableControls(navigationRoot))
        {
            if (ReferenceEquals(candidate, focusedControl))
            {
                continue;
            }

            var candidateBounds = GetBounds(candidate, navigationRoot);
            if (candidateBounds is null)
            {
                continue;
            }

            var candidateCenter = candidateBounds.Value.Center;
            var horizontalDistance = candidateCenter.X - origin.X;
            var verticalDistance = candidateCenter.Y - origin.Y;
            var primaryDistance = direction switch
            {
                NavigationDirection.Up when verticalDistance < 0 => -verticalDistance,
                NavigationDirection.Down when verticalDistance > 0 => verticalDistance,
                NavigationDirection.Left when horizontalDistance < 0 => -horizontalDistance,
                NavigationDirection.Right when horizontalDistance > 0 => horizontalDistance,
                _ => double.PositiveInfinity
            };
            var perpendicularGap = direction is NavigationDirection.Up or NavigationDirection.Down
                ? Math.Max(0, Math.Max(originBounds.Value.Left - candidateBounds.Value.Right,
                    candidateBounds.Value.Left - originBounds.Value.Right))
                : Math.Max(0, Math.Max(originBounds.Value.Top - candidateBounds.Value.Bottom,
                    candidateBounds.Value.Top - originBounds.Value.Bottom));
            var score = primaryDistance + perpendicularGap * 1.5;
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

    private static Rect? GetBounds(Control control, Control relativeTo)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), relativeTo);
        return topLeft is null ? null : new Rect(topLeft.Value, control.Bounds.Size);
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

}
