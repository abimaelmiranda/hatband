using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;

namespace Hatband.App.ViewModels;

/// <summary>
/// Owns a preparation operation independently of the screen that requested launch.
/// </summary>
public partial class GamePreparationActivityViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _cancellation;

    public GamePreparationActivityViewModel(string gameName, CancellationToken applicationLifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        GameName = gameName;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime);
        Status = string.Format(CultureInfo.CurrentCulture, Resources.GamePreparationStarting, gameName);
    }

    public string GameName { get; }

    public CancellationToken CancellationToken => _cancellation.Token;

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; } = true;

    public bool CanCancel => !_cancellation.IsCancellationRequested;

    public void Report(GamePreparationProgress progress)
    {
        var stage = progress.Stage switch
        {
            GamePreparationStage.InitializingPrefix => Resources.GamePreparationInitializing,
            GamePreparationStage.PreparingRegistry => Resources.GamePreparationRegistry,
            GamePreparationStage.InstallingDependency => Resources.GamePreparationInstalling,
            GamePreparationStage.CopyingFiles => Resources.GamePreparationCopying,
            GamePreparationStage.Completed => Resources.GamePreparationCompleted,
            _ => throw new ArgumentOutOfRangeException(nameof(progress), progress.Stage, "Unknown preparation stage.")
        };
        Status = progress.StepName is { } stepName && progress.Stage != GamePreparationStage.PreparingRegistry
            ? $"{stage}: {stepName}"
            : stage;
        IsIndeterminate = progress.Stage == GamePreparationStage.InitializingPrefix || progress.TotalSteps == 0;
        ProgressPercent = progress.TotalSteps > 0
            ? progress.CompletedSteps * 100d / progress.TotalSteps
            : 0;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancellation.Cancel();
        Status = Resources.GamePreparationCanceling;
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _cancellation.Dispose();
    }
}
