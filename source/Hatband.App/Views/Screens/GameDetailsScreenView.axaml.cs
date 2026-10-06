using Hatband.App.Navigation;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Screens;

/// <summary>
/// Owns details-screen focus while game options and confirmations use the global modal stack.
/// </summary>
public partial class GameDetailsScreenView : FullScreenView
{
    public GameDetailsScreenView()
    {
        InitializeComponent();
    }

    public override void FocusInitial()
    {
        DirectionalFocusNavigator.Focus(PlayButton.IsEffectivelyVisible && PlayButton.IsEnabled
            ? PlayButton
            : OptionsButton);
    }

    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, NavigationInputContext context)
    {
        switch (action)
        {
            case NavigationAction.Up:
            case NavigationAction.Left:
                DirectionalFocusNavigator.Focus(PlayButton);
                return NavigationActionHandling.Handled;
            case NavigationAction.Down:
            case NavigationAction.Right:
                DirectionalFocusNavigator.Focus(OptionsButton);
                return NavigationActionHandling.Handled;
            default:
                return base.HandleNavigationAction(action, context);
        }
    }
}
