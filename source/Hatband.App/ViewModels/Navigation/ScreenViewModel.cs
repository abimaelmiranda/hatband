using Hatband.App.ViewModels;

namespace Hatband.App.ViewModels.Navigation;

/// <summary>Base for a screen instance whose active lifetime follows navigation history.</summary>
public abstract class ScreenViewModel : ViewModelBase
{
    private bool _isActive;

    /// <summary>True only while this screen is the active screen in the navigation coordinator.</summary>
    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    /// <summary>Keyboard guidance displayed by the application shell while this screen is active.</summary>
    public virtual string KeyboardHelpText => string.Empty;

    internal void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        OnActivated();
    }

    internal void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        OnDeactivated();
    }

    internal void Discard()
    {
        Deactivate();
        OnDiscarded();
    }

    /// <summary>Called when navigation makes this screen the active screen.</summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>Called before another screen becomes active or this history entry is removed.</summary>
    protected virtual void OnDeactivated()
    {
    }

    /// <summary>Called when this instance is removed from navigation history; the instance may later be reused.</summary>
    protected virtual void OnDiscarded()
    {
    }
}
