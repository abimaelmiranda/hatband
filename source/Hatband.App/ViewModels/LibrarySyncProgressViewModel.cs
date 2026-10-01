using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Sync;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels;

public partial class LibrarySyncProgressViewModel : ViewModelBase
{
    private const int MaxGameNameLength = 28;

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

    public LibrarySyncProgressViewModel(
        IGameLibrarySyncService gameLibrarySyncService,
        IGameTimeToBeatSyncService gameTimeToBeatSyncService)
    {
        gameLibrarySyncService.LibraryEnrichmentProgressChanged += OnLibraryEnrichmentProgressChanged;
        gameTimeToBeatSyncService.ProgressChanged += OnTimeToBeatSyncProgressChanged;
    }

    public event Action<Game>? UpdatedGame;

    public event Action<string>? CompletionError;

    private void OnLibraryEnrichmentProgressChanged(object? sender, GameLibraryEnrichmentProgressEventArgs args)
    {
        Dispatcher.UIThread.Post(() => ApplyLibraryEnrichmentProgress(args));
    }

    private void ApplyLibraryEnrichmentProgress(GameLibraryEnrichmentProgressEventArgs args)
    {
        if (args.UpdatedGame is not null)
        {
            UpdatedGame?.Invoke(args.UpdatedGame);
        }

        LibraryEnrichmentProgressPercent = CalculateProgressPercent(args.CompletedGames, args.TotalGames);

        if (args.IsComplete)
        {
            IsLibraryEnrichmentRunning = false;
            var completionStatus = args.FailedGames == 0
                ? string.Format(CultureInfo.CurrentCulture, Resources.MetadataUpdated, args.SourceId)
                : string.Format(CultureInfo.CurrentCulture, Resources.MetadataFailed, args.SourceId, args.FailedGames);
            LibraryEnrichmentStatus = completionStatus;

            if (args.FailedGames > 0)
            {
                CompletionError?.Invoke(completionStatus);
            }

            return;
        }

        IsLibraryEnrichmentRunning = args.TotalGames > 0;
        LibraryEnrichmentStatus = FormatLibraryEnrichmentStatus(args);
    }

    private void OnTimeToBeatSyncProgressChanged(object? sender, GameTimeToBeatSyncProgressEventArgs args)
    {
        Dispatcher.UIThread.Post(() => ApplyTimeToBeatProgress(args));
    }

    private void ApplyTimeToBeatProgress(GameTimeToBeatSyncProgressEventArgs args)
    {
        if (args.UpdatedGame is not null)
        {
            UpdatedGame?.Invoke(args.UpdatedGame);
        }

        TimeToBeatSyncProgressPercent = CalculateProgressPercent(args.CompletedGames, args.TotalGames);

        if (args.IsComplete)
        {
            IsTimeToBeatSyncRunning = false;
            var completionStatus = args.FailedGames == 0
                ? Resources.HltbSyncComplete
                : string.Format(CultureInfo.CurrentCulture, Resources.HltbSyncFailed, args.FailedGames);
            TimeToBeatSyncStatus = completionStatus;

            if (args.FailedGames > 0)
            {
                CompletionError?.Invoke(completionStatus);
            }

            return;
        }

        IsTimeToBeatSyncRunning = args.TotalGames > 0;
        TimeToBeatSyncStatus = args.CurrentGameName is null
            ? string.Format(CultureInfo.CurrentCulture, Resources.HltbSyncPreparing, args.TotalGames)
            : string.Format(
                CultureInfo.CurrentCulture,
                Resources.HltbSyncProgress,
                args.CompletedGames,
                args.TotalGames,
                TruncateGameName(args.CurrentGameName));
    }

    private static string FormatLibraryEnrichmentStatus(GameLibraryEnrichmentProgressEventArgs args)
    {
        var gameName = args.CurrentGameName;

        if (args.Stage == GameLibrarySyncStage.Artwork)
        {
            return gameName is null
                ? string.Format(CultureInfo.CurrentCulture, Resources.ArtworkPreparing, args.SourceId, args.TotalGames)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.ArtworkProgress,
                    args.SourceId,
                    args.CompletedGames,
                    args.TotalGames,
                    TruncateGameName(gameName));
        }

        return gameName is null
            ? string.Format(CultureInfo.CurrentCulture, Resources.MetadataPreparing, args.SourceId, args.TotalGames)
            : string.Format(
                CultureInfo.CurrentCulture,
                Resources.MetadataProgress,
                args.SourceId,
                args.CompletedGames,
                args.TotalGames,
                TruncateGameName(gameName));
    }

    private static double CalculateProgressPercent(int completedGames, int totalGames)
    {
        return totalGames == 0 ? 0 : completedGames * 100d / totalGames;
    }

    private static string TruncateGameName(string gameName)
    {
        return gameName.Length <= MaxGameNameLength
            ? gameName
            : $"{gameName[..MaxGameNameLength]}…";
    }
}
