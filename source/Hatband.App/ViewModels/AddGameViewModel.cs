using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class AddGameViewModel(IGameLibraryService gameLibraryService) : ViewModelBase
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
            InstallDirectory = trimmedInstallDirectory,
            IsInstalled = trimmedInstallDirectory is not null
        };

        if (!string.IsNullOrWhiteSpace(LaunchTarget))
        {
            game.LaunchActions.Add(new GameLaunchAction
            {
                Name = "Play",
                Target = LaunchTarget.Trim(),
                Type = GameLaunchActionType.Executable,
                WorkingDirectory = trimmedInstallDirectory,
                IsPrimary = true
            });
        }

        try
        {
            await gameLibraryService.AddGameAsync(game, cancellationToken);
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
