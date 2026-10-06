using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.Views;

namespace Hatband.App.Views.Navigation;

/// <summary>Shared focus restoration and directional-key behavior for modal views.</summary>
public abstract class ModalView : UserControl, INavigationView
{
    private Control? _focusedControl;
    private bool _isActive;

    protected ModalView()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        Loaded += OnLoaded;
    }

    public Control NavigationRoot => this;
    public virtual bool SupportsTabNavigation => false;

    /// <summary>Routes arrows inside this modal and preserves native text, list, and open combo-box behavior.</summary>
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

    /// <summary>Handles a modal-specific Back action before the coordinator cancels this modal.</summary>
    public virtual bool TryHandleBack() => false;

    /// <summary>Focuses the initial modal target or restores focus when returning from an owned child modal.</summary>
    public virtual void FocusInitial()
    {
        var target = GetInitialFocusTarget();
        if (target is not null && target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
        {
            target.Focus(NavigationMethod.Directional);
        }
    }

    protected virtual Control? GetInitialFocusTarget()
    {
        return this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
    }

    protected virtual void OnViewActivated()
    {
    }

    protected virtual void OnViewDeactivated()
    {
    }

    /// <summary>Restores focus after this cached modal view is mounted as the active modal.</summary>
    public void ActivateView()
    {
        _isActive = true;
        OnViewActivated();
        RestoreFocus();
    }

    /// <summary>Saves the focused descendant before an owned modal covers this view.</summary>
    public void DeactivateView()
    {
        _focusedControl = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.IsFocused) ?? _focusedControl;
        _isActive = false;
        OnViewDeactivated();
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
