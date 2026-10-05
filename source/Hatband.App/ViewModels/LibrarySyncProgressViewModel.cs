using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;

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
