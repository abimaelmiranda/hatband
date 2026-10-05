using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.Core.Models.Libraries;

namespace Hatband.App.ViewModels;

public partial class AddGameViewModel : ViewModelBase
{
    private readonly IGameRepository _gameRepository;
    private readonly IGameLibraryRepository _libraryRepository;

    public AddGameViewModel(IGameRepository gameRepository, IGameLibraryRepository libraryRepository)
    {
        _gameRepository = gameRepository;
        _libraryRepository = libraryRepository;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? InstallDirectory { get; set; }

    [ObservableProperty]
    public partial string? LaunchTarget { get; set; }

    public event Action<AddGameCreationResult>? CreationCompleted;

    [RelayCommand]
    private async Task SaveGameAsync(CancellationToken cancellationToken)
    {
        var trimmedName = Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            CreationCompleted?.Invoke(new AddGameCreationResult.InvalidName());
            return;
        }

        var trimmedInstallDirectory = string.IsNullOrWhiteSpace(InstallDirectory)
            ? null
            : InstallDirectory.Trim();
        var game = new Game
        {
            Name = trimmedName,
            SourceId = GameSourceId.Manual,
            InstallationInfo = trimmedInstallDirectory is null
                ? null
                : new GameInstallationInfo { InstallDirectory = trimmedInstallDirectory }
        };

        if (!string.IsNullOrWhiteSpace(LaunchTarget))
        {
            game.GameActions.Add(new GameAction
            {
                Name = "Play",
                Target = LaunchTarget.Trim(),
                Type = GameActionType.Executable,
                WorkingDirectory = trimmedInstallDirectory,
                IsPrimary = true
            });
        }

        try
        {
            var libraries = await _libraryRepository.GetAllAsync(cancellationToken);
            var library = libraries.FirstOrDefault();
            if (library is null)
            {
                library = new GameLibrary();
                await _libraryRepository.AddAsync(library, cancellationToken);
            }

            await _gameRepository.AddAsync(library.Id, game, cancellationToken);
            CreationCompleted?.Invoke(new AddGameCreationResult.Saved(game));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CreationCompleted?.Invoke(new AddGameCreationResult.Failed(exception));
        }
    }

    public void PrepareFromExecutable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        Name = Path.GetFileNameWithoutExtension(executablePath);
        InstallDirectory = Path.GetDirectoryName(executablePath);
        LaunchTarget = executablePath;
    }

    public void Reset()
    {
        Name = string.Empty;
        InstallDirectory = null;
        LaunchTarget = null;
    }
}
