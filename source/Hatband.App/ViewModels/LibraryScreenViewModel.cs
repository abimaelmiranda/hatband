using Hatband.App.Localization;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Presents the shared library and emits workflow requests without owning a separate navigation history.
/// </summary>
public sealed class LibraryScreenViewModel : ScreenViewModel
{
    public LibraryScreenViewModel(LibrarySessionViewModel session)
    {
        Session = session;
    }

    public LibrarySessionViewModel Session { get; }

    public override string KeyboardHelpText => Resources.KeyboardChooseOpen;

    public event Action<GameCardViewModel>? GameOpened;
    public event Action<MenuAction>? MenuActionRequested;

    public void OpenSelectedGame()
    {
        if (Session.SelectedGameCard is { } game)
        {
            GameOpened?.Invoke(game);
        }
    }

    public void RequestMenuAction(MenuAction action)
    {
        MenuActionRequested?.Invoke(action);
    }
}
