using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Hatband.Core.Abstractions.Settings;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Authentication;
using Hatband.Plugins.Steam.Authentication;
using Hatband.Plugins.Steam.Localization;
using Hatband.Plugins.Steam.Settings;
using Hatband.Plugins.Steam.Store;
using Hatband.SDK.UI.Connectors;

namespace Hatband.Plugins.Steam.Connectors;

public sealed class SteamConnectorViewModel : INotifyPropertyChanged
{
    private readonly SteamStoreIntegration steamStore;
    private readonly ISettingsApi settingsApi;
    private readonly ConnectorUiContext context;
    private readonly SemaphoreSlim settingsGate = new(1, 1);
    private CancellationTokenSource? loginCancellation;
    private string connectionStatus = SteamResources.NotConnected;
    private string? activeAccountName;
    private Uri? qrCodeUri;
    private bool isLoginPending;
    private bool isSyncPending;
    private bool isSilentModeEnabled;
    private bool persistedSilentModeEnabled;
    private bool hasLoadedSettings;
    private long settingsEditRevision;
    private long loginGeneration;
    private string? settingsStatus;

    public SteamConnectorViewModel(
        SteamStoreIntegration steamStore,
        ISettingsApi settingsApi,
        ConnectorUiContext context)
    {
        ArgumentNullException.ThrowIfNull(steamStore);
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(context);
        this.steamStore = steamStore;
        this.settingsApi = settingsApi;
        this.context = context;
        RefreshConnectionStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ConnectionStatus
    {
        get => connectionStatus;
        private set => SetProperty(ref connectionStatus, value);
    }

    public string? ActiveAccountName
    {
        get => activeAccountName;
        private set => SetProperty(ref activeAccountName, value);
    }

    public Uri? QrCodeUri
    {
        get => qrCodeUri;
        private set => SetProperty(ref qrCodeUri, value);
    }

    public bool IsLoginPending
    {
        get => isLoginPending;
        private set
        {
            if (SetProperty(ref isLoginPending, value))
            {
                OnPropertyChanged(nameof(CanConnect));
            }
        }
    }

    public bool IsSyncPending
    {
        get => isSyncPending;
        private set
        {
            if (SetProperty(ref isSyncPending, value))
            {
                OnPropertyChanged(nameof(CanConnect));
                OnPropertyChanged(nameof(CanSync));
                OnPropertyChanged(nameof(CanDisconnect));
            }
        }
    }

    public bool IsConnected => steamStore.CurrentAccount is not null;

    public bool CanConnect => !IsConnected && !IsLoginPending && !IsSyncPending;

    public bool CanSync => IsConnected && !IsSyncPending;

    public bool CanDisconnect => IsConnected && !IsSyncPending;

    public bool IsSilentModeEnabled
    {
        get => isSilentModeEnabled;
        private set => SetProperty(ref isSilentModeEnabled, value);
    }

    public string? SettingsStatus
    {
        get => settingsStatus;
        private set => SetProperty(ref settingsStatus, value);
    }

    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        RefreshConnectionStatus();
        if (hasLoadedSettings)
        {
            return;
        }

        var editRevision = settingsEditRevision;
        await settingsGate.WaitAsync(cancellationToken);
        try
        {
            if (hasLoadedSettings)
            {
                return;
            }

            var settings = await settingsApi.GetSectionAsync<SteamSettings>(cancellationToken);
            persistedSilentModeEnabled = settings.SilentModeEnabled;
            if (editRevision == settingsEditRevision)
            {
                IsSilentModeEnabled = settings.SilentModeEnabled;
                persistedSilentModeEnabled = settings.SilentModeEnabled;
            }
            else
            {
                settings.SilentModeEnabled = IsSilentModeEnabled;
                await settingsApi.SaveSectionAsync(settings, cancellationToken);
                persistedSilentModeEnabled = settings.SilentModeEnabled;
            }

            hasLoadedSettings = true;
            SettingsStatus = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SettingsStatus = exception.Message;
        }
        finally
        {
            settingsGate.Release();
        }
    }

    public async Task SetSilentModeAsync(bool value)
    {
        if (IsSilentModeEnabled == value)
        {
            return;
        }

        IsSilentModeEnabled = value;
        settingsEditRevision++;
        var editRevision = settingsEditRevision;
        SettingsStatus = null;
        await settingsGate.WaitAsync();
        try
        {
            var settings = await settingsApi.GetSectionAsync<SteamSettings>();
            settings.SilentModeEnabled = IsSilentModeEnabled;
            var savedValue = settings.SilentModeEnabled;
            await settingsApi.SaveSectionAsync(settings);
            persistedSilentModeEnabled = savedValue;
            hasLoadedSettings = true;
            if (editRevision == settingsEditRevision)
            {
                SettingsStatus = null;
            }
        }
        catch (Exception exception)
        {
            if (editRevision == settingsEditRevision)
            {
                IsSilentModeEnabled = persistedSilentModeEnabled;
                SettingsStatus = exception.Message;
            }
        }
        finally
        {
            settingsGate.Release();
        }
    }

    public void Deactivate()
    {
        Interlocked.Increment(ref loginGeneration);
        loginCancellation?.Cancel();
    }

    public async Task ConnectAsync()
    {
        if (IsLoginPending)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        loginCancellation = cancellation;
        var challengeGeneration = Interlocked.Increment(ref loginGeneration);
        IsLoginPending = true;
        ConnectionStatus = SteamResources.Connecting;

        try
        {
            ConnectorAccount account;
            await using (var loginSession = await steamStore.BeginQrLoginAsync(cancellation.Token))
            {
                EventHandler<SteamQrChallengeUriChangedEventArgs> challengeHandler =
                    (sender, args) => OnChallengeUriChanged(challengeGeneration, args);
                loginSession.ChallengeUriChanged += challengeHandler;
                try
                {
                    QrCodeUri = loginSession.ChallengeUri;
                    ConnectionStatus = SteamResources.ScanQr;
                    account = await loginSession.WaitForAuthenticationAsync(cancellation.Token);
                }
                finally
                {
                    loginSession.ChallengeUriChanged -= challengeHandler;
                }
            }

            Interlocked.Increment(ref loginGeneration);
            loginCancellation = null;
            IsLoginPending = false;
            QrCodeUri = null;
            ActiveAccountName = account.DisplayName;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanSync));
            OnPropertyChanged(nameof(CanDisconnect));
            IsSyncPending = true;
            ConnectionStatus = steamStore.IsPersisted
                ? SteamResources.Format(nameof(SteamResources.ConnectedAs), account.DisplayName)
                : $"{SteamResources.Format(nameof(SteamResources.ConnectedAs), account.DisplayName)} {SteamResources.SessionTemporary}";
            await SynchronizeAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            QrCodeUri = null;
            ConnectionStatus = SteamResources.LoginCancelled;
        }
        catch (Exception exception)
        {
            QrCodeUri = null;
            ConnectionStatus = steamStore.CurrentAccount is null
                ? SteamResources.Format(nameof(SteamResources.LoginSyncFailed), exception.Message)
                : SteamResources.Format(nameof(SteamResources.SyncFailed), exception.Message);
        }
        finally
        {
            Interlocked.Increment(ref loginGeneration);
            loginCancellation = null;
            IsLoginPending = false;
            IsSyncPending = false;
        }
    }

    public void CancelLogin() => loginCancellation?.Cancel();

    public async Task SyncAsync()
    {
        if (!CanSync)
        {
            return;
        }

        IsSyncPending = true;
        ConnectionStatus = SteamResources.SyncingLibrary;
        try
        {
            await SynchronizeAsync();
        }
        catch (Exception exception)
        {
            ConnectionStatus = SteamResources.Format(nameof(SteamResources.SyncFailed), exception.Message);
        }
        finally
        {
            IsSyncPending = false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!CanDisconnect)
        {
            return;
        }

        try
        {
            await steamStore.DisconnectAsync();
            ActiveAccountName = null;
            ConnectionStatus = SteamResources.AccountDisconnected;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanSync));
            OnPropertyChanged(nameof(CanDisconnect));
        }
        catch (Exception exception)
        {
            ConnectionStatus = SteamResources.Format(nameof(SteamResources.DisconnectFailed), exception.Message);
        }
    }

    private async Task SynchronizeAsync()
    {
        await context.SyncAsync(GameSourceIds.Steam, CancellationToken.None);
        ConnectionStatus = SteamResources.SyncComplete;
    }

    private void RefreshConnectionStatus()
    {
        if (IsLoginPending || IsSyncPending)
        {
            return;
        }

        var account = steamStore.CurrentAccount;
        ActiveAccountName = account?.DisplayName;
        ConnectionStatus = account is null
            ? SteamResources.NotConnected
            : steamStore.IsPersisted
                ? SteamResources.Format(nameof(SteamResources.ConnectedAs), account.DisplayName)
                : $"{SteamResources.Format(nameof(SteamResources.ConnectedAs), account.DisplayName)} {SteamResources.SessionTemporary}";
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanSync));
        OnPropertyChanged(nameof(CanDisconnect));
    }

    private void OnChallengeUriChanged(long generation, SteamQrChallengeUriChangedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == Volatile.Read(ref loginGeneration) && IsLoginPending)
            {
                QrCodeUri = args.ChallengeUri;
            }
        });
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
