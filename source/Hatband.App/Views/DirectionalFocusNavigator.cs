using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Hatband.App.Views;

internal sealed class DirectionalFocusNavigator(Window window)
{
    public bool MoveFocus(Control navigationRoot, Key key)
    {
        var focusedControl = navigationRoot.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.IsFocused);
        if (focusedControl is null || IsNativeArrowControl(focusedControl))
        {
            return false;
        }

        var direction = GetArrowDirection(key);
        if (direction is null)
        {
            return false;
        }

        var currentCenter = GetCenter(focusedControl);
        if (currentCenter is null)
        {
            return false;
        }

        var currentPoint = currentCenter.Value;
        Control? nextControl = null;
        var shortestDistance = double.MaxValue;
        foreach (var candidate in navigationRoot.GetVisualDescendants().OfType<Control>())
        {
            if (!IsNavigationTarget(candidate))
            {
                continue;
            }

            var candidateCenter = GetCenter(candidate);
            if (candidateCenter is null || !IsInDirection(currentPoint, candidateCenter.Value, direction.Value))
            {
                continue;
            }

            var distance = GetDirectionalDistance(currentPoint, candidateCenter.Value, direction.Value);
            if (distance >= shortestDistance)
            {
                continue;
            }

            shortestDistance = distance;
            nextControl = candidate;
        }

        if (nextControl is null)
        {
            return false;
        }

        nextControl.Focus(NavigationMethod.Directional);
        return true;
    }

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

    private Point? GetCenter(Control control)
    {
        return control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window);
    }

    private bool IsNavigationTarget(Control control)
    {
        return control != window &&
               control.Focusable &&
               control.IsTabStop &&
               control.IsEffectivelyVisible &&
               control.IsHitTestVisible &&
               control.Bounds.Width > 0 &&
               control.Bounds.Height > 0 &&
               GetCenter(control) is not null;
    }

    private static bool IsNativeArrowControl(Control control)
    {
        return control is ComboBox || control.GetVisualAncestors().OfType<ComboBox>().Any();
    }

    private static Direction? GetArrowDirection(Key key)
    {
        return key switch
        {
            Key.Up => Direction.Up,
            Key.Down => Direction.Down,
            Key.Left => Direction.Left,
            Key.Right => Direction.Right,
            _ => null
        };
    }

    private static bool IsInDirection(Point current, Point candidate, Direction direction)
    {
        return direction switch
        {
            Direction.Up => candidate.Y < current.Y,
            Direction.Down => candidate.Y > current.Y,
            Direction.Left => candidate.X < current.X,
            Direction.Right => candidate.X > current.X,
            _ => false
        };
    }

    private static double GetDirectionalDistance(Point current, Point candidate, Direction direction)
    {
        var primaryDistance = direction is Direction.Up or Direction.Down
            ? Math.Abs(current.Y - candidate.Y)
            : Math.Abs(current.X - candidate.X);
        var secondaryDistance = direction is Direction.Up or Direction.Down
            ? Math.Abs(current.X - candidate.X)
            : Math.Abs(current.Y - candidate.Y);

        return primaryDistance + secondaryDistance * 2;
    }

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}
