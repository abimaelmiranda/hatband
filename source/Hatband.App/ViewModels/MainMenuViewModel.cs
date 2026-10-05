using System.Collections.ObjectModel;
using Hatband.App.Localization;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Returns a chosen application action to the shell after the global menu modal closes.
/// </summary>
public sealed class MainMenuViewModel : ModalViewModel<MenuAction>
{
    public MainMenuViewModel(bool showingHiddenGames)
    {
        MenuOptions = [
            new(Resources.MenuLibrary, MenuAction.Library),
            new(showingHiddenGames ? Resources.MenuShowAllGames : Resources.MenuViewHiddenGames, MenuAction.HiddenGames),
            new(Resources.MenuAddGame, MenuAction.AddGame),
            new(Resources.MenuSettings, MenuAction.Settings),
            new(Resources.MenuExit, MenuAction.Exit)
        ];
    }

    public ObservableCollection<MenuOptionViewModel> MenuOptions { get; }

    public override string KeyboardHelpText => Resources.KeyboardMenuNavigate;

    public void Activate(MenuAction action)
    {
        Complete(action);
    }
}
