using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.Core.Models.Libraries;

namespace Hatband.App.ViewModels;

public partial class AddGameViewModel(IGameRepository gameRepository, IGameLibraryRepository libraryRepository) : ViewModelBase
{
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
            var libraries = await libraryRepository.GetAllAsync(cancellationToken);
            var library = libraries.FirstOrDefault();
            if (library is null)
            {
                library = new GameLibrary();
                await libraryRepository.AddAsync(library, cancellationToken);
            }

            await gameRepository.AddAsync(library.Id, game, cancellationToken);
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
