using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Models.Authentication;
using QRCoder;

namespace Hatband.App.ViewModels;

public partial class ConnectorLoginViewModel : ViewModelBase
{
    private readonly string sourceId;
    private readonly string displayName;
    private readonly IQrCodeLoginProvider loginProvider;
    private readonly IConnectorSessionProvider sessionProvider;
    private Func<string, CancellationToken, Task<int>>? synchronizeLibrary;
    private CancellationTokenSource? loginCancellation;
    private bool isLibrarySyncRunning;

    [ObservableProperty]
    public partial Bitmap? QrCode { get; set; }

    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = Resources.NotConnected;

    [ObservableProperty]
    public partial string? ActiveAccountName { get; set; }

    [ObservableProperty]
    public partial bool IsLoginPending { get; set; }

    public ConnectorLoginViewModel(
        string sourceId,
        string displayName,
        IQrCodeLoginProvider loginProvider,
        IConnectorSessionProvider sessionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(sessionProvider);
        this.sourceId = sourceId;
        this.displayName = displayName;
        this.loginProvider = loginProvider;
        this.sessionProvider = sessionProvider;
        RefreshConnectionStatus();
    }

    public string SourceId => sourceId;

    public string DisplayName => displayName;

    public event Action<string>? ErrorOccurred;

    public event Action<string>? Disconnected;

    public bool CanStartLogin => !IsLoginPending;

    public bool IsConnected => ActiveAccountName is not null;

    public bool CanSynchronizeLibrary => CurrentAccount is not null && synchronizeLibrary is not null &&
        !isLibrarySyncRunning;

    public ConnectorAccount? CurrentAccount => sessionProvider.CurrentAccount;

    partial void OnIsLoginPendingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartLogin));
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnActiveAccountNameChanged(string? value)
    {
        OnPropertyChanged(nameof(IsConnected));
    }

    [RelayCommand(CanExecute = nameof(CanStartLogin))]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loginCancellation = linkedCancellation;
        IsLoginPending = true;
        ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectorConnecting, displayName);

        try
        {
            await using var session = await loginProvider.BeginQrLoginAsync(linkedCancellation.Token);
            session.ChallengeUriChanged += OnChallengeUriChanged;
            UpdateQrCode(session.ChallengeUri);
            ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectorScanQr, displayName);

            try
            {
                var authenticatedSession = await session.WaitForAuthenticationAsync(linkedCancellation.Token);
                var profile = authenticatedSession.Profile;
                ActiveAccountName = profile.DisplayName;
                SetQrCode(null);
                ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectedSyncing, profile.DisplayName);
                if (!authenticatedSession.IsPersisted)
                {
                    ConnectionStatus += " " + Resources.ConnectorSessionTemporary;
                }

                // Authentication follows this screen's lifetime; library sync belongs to the app session.
                var gameCount = await SynchronizeLibraryAsync(CancellationToken.None);
                ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectedAs, profile.DisplayName)
                    + " " + FormatSyncStatus(displayName, gameCount);
                if (sessionProvider.CurrentSession is { IsPersisted: false })
                {
                    ConnectionStatus += " " + Resources.ConnectorSessionTemporary;
                }
            }
            finally
            {
                session.ChallengeUriChanged -= OnChallengeUriChanged;
                SetQrCode(null);
            }
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            ConnectionStatus = Resources.ConnectionCancelled;
            SetQrCode(null);
        }
        catch (Exception exception)
        {
            var loginFailed = sessionProvider.CurrentAccount is null;
            ConnectionStatus = loginFailed
                ? string.Format(CultureInfo.CurrentCulture, Resources.ConnectorLoginFailure, displayName)
                : string.Format(CultureInfo.CurrentCulture, Resources.ConnectorSyncAfterLoginFailure, displayName);
            if (sessionProvider.CurrentSession is { IsPersisted: false })
            {
                ConnectionStatus += " " + Resources.ConnectorSessionTemporary;
            }

            var errorMessage = loginFailed
                ? Resources.ConnectorLoginError
                : Resources.ConnectorSyncError;
            ErrorOccurred?.Invoke(string.Format(CultureInfo.CurrentCulture, errorMessage, displayName, exception.Message));
        }
        finally
        {
            loginCancellation = null;
            IsLoginPending = false;
        }
    }

    [RelayCommand]
    private void CancelPendingLogin()
    {
        loginCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task DisconnectAccountAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DisconnectAsync(cancellationToken);
            Disconnected?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.ConnectorDisconnected, displayName));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectorSyncAfterLoginFailure, displayName);
            ErrorOccurred?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.ConnectorSyncError,
                displayName,
                exception.Message));
        }
    }

    public void CancelLogin()
    {
        loginCancellation?.Cancel();
    }

    public void RefreshConnectionStatus()
    {
        var account = sessionProvider.CurrentAccount;
        ActiveAccountName = account?.DisplayName;
        ConnectionStatus = account is not null
            ? string.Format(CultureInfo.CurrentCulture, Resources.ConnectedAs, account.DisplayName)
            : Resources.NotConnected;
    }

    public async Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var restored = await sessionProvider.RestoreAsync(cancellationToken);
        RefreshConnectionStatus();
        return restored;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await sessionProvider.DisconnectAsync(cancellationToken);
        ActiveAccountName = null;
        ConnectionStatus = Resources.Disconnected;
        SetQrCode(null);
    }

    public void SetLibrarySyncCallback(Func<string, CancellationToken, Task<int>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        synchronizeLibrary = callback;
        OnPropertyChanged(nameof(CanSynchronizeLibrary));
        SyncLibraryCommand.NotifyCanExecuteChanged();
    }

    public void SetLibrarySyncRunning(bool value)
    {
        if (isLibrarySyncRunning == value)
        {
            return;
        }

        isLibrarySyncRunning = value;
        OnPropertyChanged(nameof(CanSynchronizeLibrary));
        SyncLibraryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSynchronizeLibrary))]
    private async Task SyncLibraryAsync(CancellationToken cancellationToken)
    {
        var account = CurrentAccount;
        if (account is null)
        {
            return;
        }

        try
        {
            ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.SyncingAccount, account.DisplayName);
            var gameCount = await SynchronizeLibraryAsync(cancellationToken);
            ConnectionStatus = FormatSyncStatus(displayName, gameCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.ConnectorSyncError,
                displayName,
                exception.Message));
        }
    }

    public static string FormatSyncStatus(string displayName, int gameCount)
    {
        return gameCount == 0
            ? string.Format(CultureInfo.CurrentCulture, Resources.ConnectorSyncEmpty, displayName)
            : string.Format(CultureInfo.CurrentCulture, Resources.ConnectorSyncCompleteCount, gameCount);
    }

    private Task<int> SynchronizeLibraryAsync(CancellationToken cancellationToken)
    {
        var callback = synchronizeLibrary
            ?? throw new InvalidOperationException($"The {displayName} library synchronization callback has not been configured.");
        return callback(sourceId, cancellationToken);
    }

    private void OnChallengeUriChanged(object? sender, QrChallengeUriChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => UpdateQrCode(e.ChallengeUri));
    }

    private void UpdateQrCode(Uri challengeUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(challengeUri.AbsoluteUri, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        using var stream = new MemoryStream(renderer.GetGraphic(8));
        SetQrCode(new Bitmap(stream));
    }

    private void SetQrCode(Bitmap? qrCode)
    {
        var previousCode = QrCode;
        QrCode = qrCode;
        previousCode?.Dispose();
    }
}
