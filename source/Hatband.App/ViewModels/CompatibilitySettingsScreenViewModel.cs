using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>Edits per-game compatibility settings from the game's options screen.</summary>
public partial class CompatibilitySettingsScreenViewModel : ScreenViewModel
{
    private readonly IGameRepository _gameRepository;
    private Game? _game;

    public CompatibilitySettingsScreenViewModel(
        IGameRepository gameRepository,
        CompatibilityEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(editor);
        _gameRepository = gameRepository;
        Editor = editor;
    }

    public CompatibilityEditorViewModel Editor { get; }

    [ObservableProperty]
    public partial string GameName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event Action? CancelRequested;

    public event EventHandler<Game>? Saved;

    public void Load(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.SourceId != GameSourceId.Manual)
        {
            throw new ArgumentException("Compatibility settings are only available for manual games.", nameof(game));
        }

        _game = game;
        GameName = game.Name;
        ErrorMessage = null;
        Editor.Load(game.CompatibilityTool, game.CompatibilityPrefix, game.Id);
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke();

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var game = _game;
        if (game is null)
        {
            throw new InvalidOperationException("A manual game must be loaded before saving compatibility settings.");
        }

        var validationError = Editor.ValidateConfiguration();
        if (validationError is not null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        var originalTool = game.CompatibilityTool;
        var originalPrefix = game.CompatibilityPrefix;
        try
        {
            game.CompatibilityTool = Editor.ConfiguredTool;
            game.CompatibilityPrefix = Editor.CreatePrefix(game.Id);
            await _gameRepository.UpdateAsync(game, cancellationToken);
            Saved?.Invoke(this, game);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            game.CompatibilityTool = originalTool;
            game.CompatibilityPrefix = originalPrefix;
            throw;
        }
        catch (Exception exception)
        {
            game.CompatibilityTool = originalTool;
            game.CompatibilityPrefix = originalPrefix;
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.CompatibilitySaveError, exception.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
