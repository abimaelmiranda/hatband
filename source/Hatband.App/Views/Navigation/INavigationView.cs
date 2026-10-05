using Avalonia.Controls;
using Avalonia.Input;
using Hatband.App.Navigation;

namespace Hatband.App.Views.Navigation;

/// <summary>Navigation behavior exposed by the one mounted screen or modal view.</summary>
public interface INavigationView
{
    /// <summary>Routes one semantic action while preserving native control behavior when requested.</summary>
    NavigationActionHandling HandleNavigationAction(NavigationAction action, KeyEventArgs originalEvent);

    /// <summary>Gives the active view a chance to handle Back before the coordinator dismisses or pops.</summary>
    bool TryHandleBack(KeyEventArgs originalEvent);

    /// <summary>Root used for directional focus movement and bounds.</summary>
    Control NavigationRoot { get; }

    /// <summary>Focuses this view's initial target.</summary>
    void FocusInitial();
}
