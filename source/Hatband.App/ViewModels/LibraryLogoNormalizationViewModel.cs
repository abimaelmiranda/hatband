using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Models.Games;
using Microsoft.Extensions.Logging;

namespace Hatband.App.ViewModels;

public partial class LibraryLogoNormalizationViewModel : ObservableObject, IDisposable
{
    private readonly IGameLogoNormalizationService normalizationService;
    private readonly ILogger<LibraryLogoNormalizationViewModel> logger;
    private readonly CancellationToken applicationLifetimeToken;
    private CancellationTokenSource? runCancellation;
    private Task? runTask;
    private bool isRequested;
    private bool rerunRequested;
    private bool disposed;
    private long runRevision;

    public LibraryLogoNormalizationViewModel(
        IGameLogoNormalizationService normalizationService,
        ILogger<LibraryLogoNormalizationViewModel> logger,
        CancellationToken applicationLifetimeToken)
    {
        ArgumentNullException.ThrowIfNull(normalizationService);
        ArgumentNullException.ThrowIfNull(logger);
        this.normalizationService = normalizationService;
        this.logger = logger;
        this.applicationLifetimeToken = applicationLifetimeToken;
    }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    public event Action<GameLogoNormalizationProgress>? GameCompleted;

    public void RequestRun()
    {
        if (disposed || applicationLifetimeToken.IsCancellationRequested)
        {
            return;
        }

        isRequested = true;
        if (runTask is { IsCompleted: false })
        {
            rerunRequested = true;
            return;
        }

        runTask = RunUntilStableAsync();
    }

    public void Cancel()
    {
        isRequested = false;
        rerunRequested = false;
        var canceledRunRevision = Interlocked.Increment(ref runRevision);
        runCancellation?.Cancel();
        SetProgressState(
            isRunning: false,
            status: null,
            progressPercent: 0,
            currentRunRevision: canceledRunRevision);
    }

    public void Dispose()
    {
        disposed = true;
        Cancel();
        GameCompleted = null;
    }

    private async Task RunUntilStableAsync()
    {
        try
        {
            while (isRequested && !applicationLifetimeToken.IsCancellationRequested)
            {
                rerunRequested = false;
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetimeToken);
                var cancellationToken = cancellation.Token;
                var currentRunRevision = Interlocked.Increment(ref runRevision);
                runCancellation = cancellation;
                SetProgressState(
                    isRunning: true,
                    status: Resources.LibraryLogoNormalizationPreparing,
                    progressPercent: 0,
                    currentRunRevision: currentRunRevision);
                try
                {
                    var progress = new CallbackProgress<GameLogoNormalizationProgress>(gameProgress =>
                        Dispatcher.UIThread.Post(() => ApplyProgress(gameProgress, cancellationToken, currentRunRevision)));
                    await normalizationService.NormalizeLibraryAsync(progress, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not normalize the library logos.");
                }
                finally
                {
                    if (ReferenceEquals(runCancellation, cancellation))
                    {
                        runCancellation = null;
                    }

                    SetProgressState(
                        isRunning: false,
                        status: null,
                        progressPercent: 0,
                        currentRunRevision: currentRunRevision);
                }

                if (!rerunRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            runTask = null;
            if (isRequested && rerunRequested && !applicationLifetimeToken.IsCancellationRequested && !disposed)
            {
                rerunRequested = false;
                RequestRun();
            }
        }
    }

    private void ApplyProgress(
        GameLogoNormalizationProgress progress,
        CancellationToken cancellationToken,
        long currentRunRevision)
    {
            if (cancellationToken.IsCancellationRequested || !isRequested ||
                currentRunRevision != Interlocked.Read(ref runRevision))
        {
            return;
        }

        if (progress.ErrorMessage is not null)
        {
            logger.LogWarning(
                "Could not normalize logo for game {GameId}: {ErrorMessage}",
                progress.GameId,
                progress.ErrorMessage);
        }

        var progressPercent = progress.TotalCount == 0
            ? 100
            : progress.CompletedCount * 100d / progress.TotalCount;
        Status = string.Format(
            CultureInfo.CurrentCulture,
            Resources.LibraryLogoNormalizationProgress,
            progress.CompletedCount,
            progress.TotalCount);
        ProgressPercent = progressPercent;
        try
        {
            GameCompleted?.Invoke(progress);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not apply normalized logo progress for game {GameId}.", progress.GameId);
        }
    }

    private void SetProgressState(bool isRunning, string? status, double progressPercent, long currentRunRevision)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (currentRunRevision != Interlocked.Read(ref runRevision))
            {
                return;
            }

            IsRunning = isRunning;
            Status = status;
            ProgressPercent = progressPercent;
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (currentRunRevision != Interlocked.Read(ref runRevision))
            {
                return;
            }

            IsRunning = isRunning;
            Status = status;
            ProgressPercent = progressPercent;
        });
    }

    private sealed class CallbackProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
