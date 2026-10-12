using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hatband.Core.Abstractions.Settings;
using Hatband.Plugins.SteamGridDB.Authentication;
using Hatband.Plugins.SteamGridDB.Settings;
using Hatband.Plugins.SteamGridDB.Localization;

namespace Hatband.Plugins.SteamGridDB.Connectors;

public sealed class SteamGridDBConnectorViewModel : INotifyPropertyChanged
{
    private readonly ISettingsApi settingsApi;
    private readonly SteamGridDBCredentialService credentials;
    private SteamGridDBMode mode = SteamGridDBMode.Scraper;
    private SteamGridDBMode selectedMode = SteamGridDBMode.Scraper;
    private string? apiKey;
    private string? status;
    private bool isKeyConfigured;
    private bool isBusy;

    public SteamGridDBConnectorViewModel(ISettingsApi settingsApi, SteamGridDBCredentialService credentials)
    {
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(credentials);
        this.settingsApi = settingsApi;
        this.credentials = credentials;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SteamGridDBMode Mode
    {
        get => mode;
        private set => SetProperty(ref mode, value);
    }

    public SteamGridDBMode SelectedMode
    {
        get => selectedMode;
        set => SetProperty(ref selectedMode, value);
    }

    public string? ApiKey
    {
        get => apiKey;
        set => SetProperty(ref apiKey, value);
    }

    public string? Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public bool IsKeyConfigured
    {
        get => isKeyConfigured;
        private set => SetProperty(ref isKeyConfigured, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        Status = null;
        try
        {
            try
            {
                var settings = await settingsApi.GetSectionAsync<SteamGridDBSettings>(cancellationToken);
                Mode = settings.Mode;
                SelectedMode = settings.Mode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                Status = SteamGridDBResources.SettingsUnavailable;
                return;
            }

            try
            {
                await RefreshKeyStateAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                Status = SteamGridDBResources.KeyStoreUnavailable;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveModeAsync()
    {
        IsBusy = true;
        Status = null;
        try
        {
            var settings = await settingsApi.GetSectionAsync<SteamGridDBSettings>();
            settings.Mode = SelectedMode;
            await settingsApi.SaveSectionAsync(settings);
            Mode = settings.Mode;
            Status = SteamGridDBResources.ModeSaved;
        }
        catch
        {
            SelectedMode = Mode;
            Status = SteamGridDBResources.ModeSaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveApiKeyAsync()
    {
        var apiKeyToSave = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKeyToSave))
        {
            Status = SteamGridDBResources.KeyRequired;
            return;
        }

        IsBusy = true;
        Status = null;
        try
        {
            await credentials.SaveAsync(apiKeyToSave);
            ApiKey = null;
            IsKeyConfigured = true;
            Status = SteamGridDBResources.KeySaved;
        }
        catch
        {
            Status = SteamGridDBResources.KeySaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteApiKeyAsync()
    {
        IsBusy = true;
        Status = null;
        try
        {
            await credentials.DeleteAsync();
            IsKeyConfigured = false;
            ApiKey = null;
            Status = SteamGridDBResources.KeyDeleted;
        }
        catch
        {
            Status = SteamGridDBResources.KeyDeleteFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshKeyStateAsync(CancellationToken cancellationToken)
    {
        IsKeyConfigured = await credentials.IsConfiguredAsync(cancellationToken);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
