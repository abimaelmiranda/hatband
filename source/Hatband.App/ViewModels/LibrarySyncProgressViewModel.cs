using Avalonia.Threading;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;

namespace Hatband.App.ViewModels;

public partial class LibrarySyncProgressViewModel : ViewModelBase
{
    private string? _librarySyncSourceName;
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
        DispatchToUi(() =>
        {
            IsLibraryEnrichmentRunning = true;
            LibraryEnrichmentProgressPercent = 0;
            LibraryEnrichmentStatus = string.Format(Resources.MetadataPreparing, sourceName, 0);
        });
    }

    public void BeginLibrarySync(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        _librarySyncSourceName = sourceName;
        ReportLibrarySyncProgress(new GameLibrarySyncProgress(GameLibrarySyncStage.RetrievingCatalog));
    }

    public void ReportLibrarySyncProgress(GameLibrarySyncProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sourceName = _librarySyncSourceName
            ?? throw new InvalidOperationException("A source name must be set before reporting library sync progress.");
        DispatchToUi(() =>
        {
            switch (progress.Stage)
            {
                case GameLibrarySyncStage.RetrievingCatalog:
                    IsLibraryEnrichmentRunning = false;
                    LibraryEnrichmentProgressPercent = 0;
                    LibraryEnrichmentStatus = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.ConnectorSyncLoadingLibrary,
                        sourceName);
                    break;
                case GameLibrarySyncStage.UpdatingGames:
                    IsLibraryEnrichmentRunning = true;
                    LibraryEnrichmentProgressPercent = progress.TotalGames == 0
                        ? 100
                        : progress.CompletedGames * 100d / progress.TotalGames;
                    LibraryEnrichmentStatus = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.ConnectorSyncUpdatingGame,
                        sourceName,
                        progress.CompletedGames,
                        progress.TotalGames,
                        progress.CurrentGameName);
                    break;
                case GameLibrarySyncStage.RefreshingLibrary:
                    IsLibraryEnrichmentRunning = true;
                    LibraryEnrichmentProgressPercent = 100;
                    LibraryEnrichmentStatus = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.ConnectorSyncRefreshingLibrary,
                        sourceName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress.Stage, "Unknown library sync stage.");
            }
        });
    }

    public void CompleteSync(string sourceName, Exception? error = null)
    {
        DispatchToUi(() =>
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

    private static void DispatchToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
