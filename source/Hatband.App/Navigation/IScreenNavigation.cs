using System.Collections.ObjectModel;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.Navigation;

/// <summary>Controls screen history and reports the screen currently presented by the application.</summary>
public interface IScreenNavigation
{
    /// <summary>The screen at the end of the navigation history, or null before initialization.</summary>
    ScreenViewModel? ActiveScreen { get; }

    /// <summary>Screen instances retained for back navigation, in presentation order.</summary>
    ReadOnlyObservableCollection<ScreenViewModel> History { get; }

    /// <summary>Raised when returning from a screen requests the shell menu to reopen.</summary>
    event EventHandler? ReturnToMenuRequested;

    /// <summary>Sets the first screen. This must be called exactly once before navigation or modal presentation.</summary>
    void Initialize(ScreenViewModel initialScreen);

    /// <summary>Pushes a new screen or resumes an existing history entry after unwinding screens above it.</summary>
    void Navigate(ScreenViewModel screen, bool returnToMenuOnBack = false);

    /// <summary>Discards current history and installs one root screen; open modal chains must be dismissed first.</summary>
    void Reset(ScreenViewModel screen);

    /// <summary>Pops the current screen and optionally honors that history entry's return-to-menu policy.</summary>
    bool GoBack(bool reopenMenu = true);
}
