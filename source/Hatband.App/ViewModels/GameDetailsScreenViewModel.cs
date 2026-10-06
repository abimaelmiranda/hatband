using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Presents game details and requests dialogs through the global modal service.
/// Each asynchronous action retains its target game even if library selection changes.
/// </summary>
public partial class GameDetailsScreenViewModel : ScreenViewModel
{
    private readonly IModalService _modals;
    private readonly IHostSystemInfo _hostSystemInfo;

    public GameDetailsScreenViewModel(LibrarySessionViewModel session, IModalService modals, IHostSystemInfo hostSystemInfo)
    {
        Session = session;
        _modals = modals;
        _hostSystemInfo = hostSystemInfo;
    }

    public LibrarySessionViewModel Session { get; }

    public override IReadOnlyList<InputHint> InputHints =>
    [
        new(NavigationAction.Up, Resources.InputHintNavigate),
        new(NavigationAction.Confirm, Resources.InputHintActivate),
        new(NavigationAction.Back, Resources.InputHintBack),
        new(NavigationAction.OpenMenu, Resources.InputHintMenu)
    ];

    public event Action<GameCardViewModel>? EditRequested;

    public event Action<GameCardViewModel>? CompatibilityRequested;

    public event Action? ManualGameDeleted;

    [RelayCommand]
    private async Task ActivatePrimaryActionAsync(CancellationToken cancellationToken)
    {
        var game = Session.SelectedGameCard;
        if (game is null || Session.IsSelectedGameManagementPending || !game.IsCompatibleWithHost)
        {
            return;
        }

        var locations = await Session.ActivatePrimaryGameActionAsync(game, cancellationToken);
        if (locations is null)
        {
            return;
        }

        var picker = new InstallLocationModalViewModel(locations);
        var completion = await _modals.ShowAsync(picker, this);
        if (completion.Outcome != ModalOutcome.Confirmed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await Session.InstallGameAsync(game.Game, picker.SelectedLocation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Session.StatusMessage = string.Format(CultureInfo.CurrentCulture, Resources.GameManagementError, exception.Message);
        }
    }

    [RelayCommand]
    private async Task OpenOptionsAsync()
    {
        if (Session.SelectedGameCard is not { } game || Session.IsSelectedGameManagementPending)
        {
            return;
        }

        var options = new GameOptionsViewModel(game, Session.CanUninstallSelectedGame, _hostSystemInfo.Platform == HostOperatingSystem.Linux);
        var completion = await _modals.ShowAsync(options, this);
        if (completion.Outcome != ModalOutcome.Confirmed)
        {
            return;
        }

        switch (completion.GetConfirmedValue())
        {
            case GameOptionAction.Edit:
                EditRequested?.Invoke(game);
                break;
            case GameOptionAction.ToggleHidden:
                await Session.ToggleGameHiddenAsync(game);
                break;
            case GameOptionAction.Uninstall:
                var confirmation = new ConfirmationModalViewModel(
                    Resources.UninstallConfirmationTitle,
                    game.Name,
                    Resources.UninstallConfirmationMessage,
                    Resources.UninstallGame);
                var confirmed = await _modals.ShowAsync(confirmation, this);
                if (confirmed.Outcome == ModalOutcome.Confirmed && confirmed.GetConfirmedValue())
                {
                    await Session.UninstallGameAsync(game);
                }
                break;
            case GameOptionAction.Delete:
                var deleteConfirmation = new ConfirmationModalViewModel(
                    Resources.DeleteGameConfirmationTitle,
                    game.Name,
                    Resources.DeleteGameConfirmationMessage,
                    Resources.DeleteGame);
                var deleteConfirmed = await _modals.ShowAsync(deleteConfirmation, this);
                if (deleteConfirmed.Outcome == ModalOutcome.Confirmed &&
                    deleteConfirmed.GetConfirmedValue() &&
                    await Session.DeleteManualGameAsync(game))
                {
                    ManualGameDeleted?.Invoke();
                }
                break;
            case GameOptionAction.Compatibility:
                CompatibilityRequested?.Invoke(game);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(completion));
        }
    }
}
