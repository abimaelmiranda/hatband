namespace Hatband.App.Navigation;

/// <summary>Semantic input commands routed from the window to its topmost navigation view.</summary>
public enum NavigationAction
{
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Back,
    OpenMenu,
    PreviousTab,
    NextTab
}

/// <summary>Indicates whether a navigation view handled an action, left it to native control behavior, or declined it.</summary>
public enum NavigationActionHandling
{
    Handled,
    Native,
    Unhandled
}
