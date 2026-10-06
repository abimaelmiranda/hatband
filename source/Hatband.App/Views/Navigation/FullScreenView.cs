using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.Views;

namespace Hatband.App.Views.Navigation;

/// <summary>Shared focus and directional-key behavior for full-screen navigation views.</summary>
public abstract class FullScreenView : UserControl, INavigationView
{
    private Control? _focusedControl;
    private bool _isActive;

    protected FullScreenView()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        Loaded += OnLoaded;
    }

    public Control NavigationRoot => this;
    public virtual bool SupportsTabNavigation => false;

    /// <summary>Routes arrows through directional focus while leaving text editing and native list controls untouched.</summary>
    public virtual NavigationActionHandling HandleNavigationAction(NavigationAction action, NavigationInputContext context)
    {
        if (action == NavigationAction.Back)
        {
            return TryHandleBack()
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        return NavigationActionHandler.Handle(NavigationRoot, action, context);
    }

    /// <summary>Handles a screen-specific Back action before shell navigation is consulted.</summary>
    public virtual bool TryHandleBack() => false;

    /// <summary>Focuses the initial target or restores the last focused descendant when this cached view returns.</summary>
    public virtual void FocusInitial()
    {
        var target = GetInitialFocusTarget();
        if (target is not null && target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
        {
            target.Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>Marks the cached view active and restores its focus after it is mounted.</summary>
    public void ActivateView()
    {
        _isActive = true;
        OnViewActivated();
        RestoreFocus();
    }

    /// <summary>Saves the focused descendant before the cached view is unmounted.</summary>
    public void DeactivateView()
    {
        var currentFocus = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.IsFocused);
        if (currentFocus is not null)
        {
            _focusedControl = currentFocus;
        }

        _isActive = false;
        OnViewDeactivated();
    }

    /// <summary>Chooses the initial focus target; override for screens with a meaningful default selection.</summary>
    protected virtual Control? GetInitialFocusTarget()
    {
        return this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
    }

    /// <summary>Hook called when this view becomes active.</summary>
    protected virtual void OnViewActivated()
    {
    }

    /// <summary>Hook called before this view is deactivated.</summary>
    protected virtual void OnViewDeactivated()
    {
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_isActive)
        {
            RestoreFocus();
        }
    }

    private void RestoreFocus()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (_focusedControl is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && _focusedControl.Focusable)
        {
            _focusedControl.Focus(NavigationMethod.Directional);
            return;
        }

        FocusInitial();
    }
}
