using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;

namespace Hatband.App.ViewModels;

public partial class LibrarySyncProgressViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsLibraryEnrichmentRunning { get; set; }

    [ObservableProperty]
    public partial string? LibraryEnrichmentStatus { get; set; }

    [ObservableProperty]
    public partial double LibraryEnrichmentProgressPercent { get; set; }

    [ObservableProperty]
    public partial bool IsTimeToBeatSyncRunning { get; set; }

    [ObservableProperty]
    public partial string? TimeToBeatSyncStatus { get; set; }

    [ObservableProperty]
    public partial double TimeToBeatSyncProgressPercent { get; set; }

    public event Action<string>? CompletionError;

    public void BeginSync(string sourceName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsLibraryEnrichmentRunning = true;
            LibraryEnrichmentProgressPercent = 0;
            LibraryEnrichmentStatus = string.Format(Resources.MetadataPreparing, sourceName, 0);
        });
    }

    public void BeginLibrarySync()
    {
        ReportLibrarySyncProgress(new GameLibrarySyncProgress(GameLibrarySyncStage.RetrievingCatalog));
    }

    public void ReportLibrarySyncProgress(GameLibrarySyncProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Dispatcher.UIThread.Post(() =>
        {
            switch (progress.Stage)
            {
                case GameLibrarySyncStage.RetrievingCatalog:
                    IsLibraryEnrichmentRunning = false;
                    LibraryEnrichmentProgressPercent = 0;
                    LibraryEnrichmentStatus = Resources.SteamSyncLoadingLibrary;
                    break;
                case GameLibrarySyncStage.UpdatingGames:
                    IsLibraryEnrichmentRunning = true;
                    LibraryEnrichmentProgressPercent = progress.TotalGames == 0
                        ? 100
                        : progress.CompletedGames * 100d / progress.TotalGames;
                    LibraryEnrichmentStatus = string.Format(
                        Resources.SteamSyncUpdatingGame,
                        progress.CompletedGames,
                        progress.TotalGames,
                        progress.CurrentGameName);
                    break;
                case GameLibrarySyncStage.RefreshingLibrary:
                    IsLibraryEnrichmentRunning = true;
                    LibraryEnrichmentProgressPercent = 100;
                    LibraryEnrichmentStatus = Resources.SteamSyncRefreshingLibrary;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress.Stage, "Unknown library sync stage.");
            }
        });
    }

    public void CompleteSync(string sourceName, Exception? error = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsLibraryEnrichmentRunning = false;
            LibraryEnrichmentProgressPercent = error is null ? 100 : 0;
            LibraryEnrichmentStatus = error is null
                ? string.Format(Resources.MetadataUpdated, sourceName)
                : string.Format(Resources.MetadataFailed, sourceName, error.Message);
            if (error is not null)
            {
                CompletionError?.Invoke(LibraryEnrichmentStatus);
            }
        });
    }
}
