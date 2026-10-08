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

public partial class SteamConnectorLoginViewModel : ViewModelBase
{
    private readonly IQrCodeLoginProvider loginProvider;
    private readonly IConnectorSessionProvider sessionProvider;
    private readonly Func<CancellationToken, Task<int>> synchronizeLibrary;
    private CancellationTokenSource? loginCancellation;

    [ObservableProperty]
    public partial Bitmap? QrCode { get; set; }

    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = Resources.NotConnected;

    [ObservableProperty]
    public partial string? ActiveAccountName { get; set; }

    [ObservableProperty]
    public partial bool IsLoginPending { get; set; }

    public SteamConnectorLoginViewModel(
        IQrCodeLoginProvider loginProvider,
        IConnectorSessionProvider sessionProvider,
        Func<CancellationToken, Task<int>> synchronizeLibrary)
    {
        this.loginProvider = loginProvider;
        this.sessionProvider = sessionProvider;
        this.synchronizeLibrary = synchronizeLibrary;
    }

    public event Action<string>? ErrorOccurred;

    public event Action<string>? Disconnected;

    public bool CanStartLogin => !IsLoginPending;

    public ConnectorAccount? CurrentAccount => sessionProvider.CurrentAccount;

    partial void OnIsLoginPendingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartLogin));
        ConnectSteamCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartLogin))]
    private async Task ConnectSteamAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loginCancellation = linkedCancellation;
        IsLoginPending = true;
        ConnectionStatus = Resources.ConnectingSteam;

        try
        {
            await using var session = await loginProvider.BeginQrLoginAsync(linkedCancellation.Token);
            session.ChallengeUriChanged += OnChallengeUriChanged;
            UpdateQrCode(session.ChallengeUri);
            ConnectionStatus = Resources.ScanQr;

            try
            {
                var authenticatedSession = await session.WaitForAuthenticationAsync(linkedCancellation.Token);
                var profile = authenticatedSession.Profile;
                ActiveAccountName = profile.DisplayName;
                SetQrCode(null);
                ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectedSyncing, profile.DisplayName);
                if (!authenticatedSession.IsPersisted)
                {
                    ConnectionStatus += " " + Resources.SteamSessionTemporary;
                }

                // Authentication follows this screen's lifetime; library sync belongs to the app session.
                var gameCount = await synchronizeLibrary(CancellationToken.None);
                ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Resources.ConnectedAs, profile.DisplayName)
                    + " " + FormatSyncStatus(gameCount);
                if (sessionProvider.CurrentSession is { IsPersisted: false })
                {
                    ConnectionStatus += " " + Resources.SteamSessionTemporary;
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
            ConnectionStatus = sessionProvider.CurrentAccount is null
                ? Resources.SteamLoginFailure
                : Resources.SteamSyncAfterLoginFailure;
            if (sessionProvider.CurrentSession is { IsPersisted: false })
            {
                ConnectionStatus += " " + Resources.SteamSessionTemporary;
            }

            ErrorOccurred?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.ConnectSteamSyncError, exception.Message));
        }
        finally
        {
            loginCancellation = null;
            IsLoginPending = false;
        }
    }

    [RelayCommand]
    private void CancelSteamLogin()
    {
        loginCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task DisconnectSteamAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DisconnectAsync(cancellationToken);
            Disconnected?.Invoke(Resources.SteamDisconnected);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.SteamSyncError,
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

    public void SetConnectionStatus(string status)
    {
        ConnectionStatus = status;
    }

    public static string FormatSyncStatus(int gameCount)
    {
        return gameCount == 0
            ? Resources.SyncCompleteEmpty
            : string.Format(CultureInfo.CurrentCulture, Resources.SyncCompleteCount, gameCount);
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
