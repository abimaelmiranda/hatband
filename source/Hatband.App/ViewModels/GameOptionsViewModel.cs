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
            new(Resources.EditGame, FluentIconGlyph.Edit, GameOptionAction.Edit),
            new(game.Game.IsHidden ? Resources.UnhideGame : Resources.HideGame, FluentIconGlyph.AppFolder, GameOptionAction.ToggleHidden)
        };
        if (canUninstall)
        {
            options.Add(new(Resources.UninstallGame, FluentIconGlyph.Delete, GameOptionAction.Uninstall));
        }
        if (game.Game.SourceId == GameSourceId.Manual)
        {
            options.Add(new(Resources.DeleteGame, FluentIconGlyph.Delete, GameOptionAction.Delete));
        }
        options.Add(new(Resources.Compatibility, FluentIconGlyph.Toolbox, GameOptionAction.Compatibility));
        Options = options;
    }

    public GameCardViewModel Game { get; }

    public IReadOnlyList<GameOptionViewModel> Options { get; }
}
