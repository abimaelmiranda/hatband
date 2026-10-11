namespace Hatband.Core.Models.Games;

public enum GamePreparationStage
{
    InitializingPrefix,
    PreparingRegistry,
    InstallingDependency,
    CopyingFiles,
    Completed
}

public sealed record GamePreparationProgress(
    GamePreparationStage Stage,
    string? StepName,
    int CompletedSteps,
    int TotalSteps);
