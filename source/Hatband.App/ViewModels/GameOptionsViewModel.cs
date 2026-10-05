using Hatband.App.Localization;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Lists the actions available for a captured game and returns the user's choice without navigating.
/// </summary>
public sealed class GameOptionsViewModel : ModalViewModel<GameOptionAction>
{
    public GameOptionsViewModel(GameCardViewModel game, bool canUninstall)
    {
        Game = game;
        var options = new List<GameOptionViewModel>
        {
            new(Resources.EditGame, "✎", GameOptionAction.Edit),
            new(game.Game.IsHidden ? Resources.UnhideGame : Resources.HideGame, "◉", GameOptionAction.ToggleHidden)
        };
        if (canUninstall)
        {
            options.Add(new(Resources.UninstallGame, "⌫", GameOptionAction.Uninstall));
        }
        options.Add(new(Resources.Compatibility, "⚙", GameOptionAction.Compatibility));
        Options = options;
    }

    public GameCardViewModel Game { get; }

    public IReadOnlyList<GameOptionViewModel> Options { get; }
}
