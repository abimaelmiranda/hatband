using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace Hatband.App.ViewModels.Settings;

public partial class ProtonManagementViewModel : ObservableObject
{
    private readonly ICompatibilityToolReleaseCatalogService _compatibilityToolReleaseCatalogService;
    private readonly ICompatibilityToolDiscoveryService _compatibilityToolDiscoveryService;
    private readonly ICompatibilityToolInstallationService _compatibilityToolInstallationService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IModalService _modalService;
    private readonly ILogger<ProtonManagementViewModel> _logger;

    [ObservableProperty]
    public partial ObservableCollection<ProtonToolViewModel> InstalledTools { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<ProtonReleaseProviderViewModel> Providers { get; set; } = [];

    [ObservableProperty]
    public partial ProtonReleaseProviderViewModel? SelectedProvider { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadedProviders { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public ProtonManagementViewModel(
        ICompatibilityToolReleaseCatalogService compatibilityToolReleaseCatalogService,
        ICompatibilityToolDiscoveryService compatibilityToolDiscoveryService,
        ICompatibilityToolInstallationService compatibilityToolInstallationService,
        IHostSystemInfo hostSystemInfo,
        IModalService modalService,
        ILogger<ProtonManagementViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(compatibilityToolReleaseCatalogService);
        ArgumentNullException.ThrowIfNull(compatibilityToolDiscoveryService);
        ArgumentNullException.ThrowIfNull(compatibilityToolInstallationService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(modalService);
        ArgumentNullException.ThrowIfNull(logger);
        _compatibilityToolReleaseCatalogService = compatibilityToolReleaseCatalogService;
        _compatibilityToolDiscoveryService = compatibilityToolDiscoveryService;
        _compatibilityToolInstallationService = compatibilityToolInstallationService;
        _hostSystemInfo = hostSystemInfo;
        _modalService = modalService;
        _logger = logger;
    }

    public bool IsLinuxSupported => _hostSystemInfo.Platform == HostOperatingSystem.Linux;

    public bool CanBrowseCatalogs => IsLinuxSupported;

    public bool IsCatalogBrowsingUnsupported => !CanBrowseCatalogs;

    public bool HasInstalledTools => InstalledTools.Count > 0;

    public bool HasNoInstalledTools => !HasInstalledTools;

    public bool HasNoProviders => Providers.Count == 0;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoErrorMessage => !HasErrorMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string UnsupportedMessage => Resources.ProtonLinuxOnly;

    public bool CanInteract => !IsInstalling;

    public int NavigationFieldCount => Providers.Count + 1;

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!CanBrowseCatalogs)
        {
            ErrorMessage = UnsupportedMessage;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await RefreshInstalledToolsAsync(cancellationToken);
            var providers = _compatibilityToolReleaseCatalogService.GetProviders();
            Providers = new ObservableCollection<ProtonReleaseProviderViewModel>(providers
                .Select(provider => new ProtonReleaseProviderViewModel(provider, OpenProviderVersionsAsync)));
            SelectProvider(null);
            HasLoadedProviders = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not refresh Proton compatibility providers.");
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Resources.ProtonRefreshError, exception.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectProvider(ProtonReleaseProviderViewModel? provider)
    {
        if (provider is not null && !Providers.Contains(provider))
        {
            throw new ArgumentException("The Proton release provider does not belong to this view model.", nameof(provider));
        }

        foreach (var availableProvider in Providers)
        {
            availableProvider.IsSelected = availableProvider == provider;
        }

        SelectedProvider = provider;
    }

    private async Task OpenProviderVersionsAsync(ProtonReleaseProviderViewModel provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!CanBrowseCatalogs || IsInstalling)
        {
            return;
        }

        SelectProvider(provider);
        var modal = new ProtonReleaseSelectionModalViewModel(
            provider.Provider,
            _compatibilityToolReleaseCatalogService);
        var completion = await _modalService.ShowAsync(modal);
        if (completion.Outcome == ModalOutcome.Confirmed)
        {
            await InstallReleaseAsync(completion.GetConfirmedValue());
        }
    }

    private async Task InstallReleaseAsync(CompatibilityToolRelease release)
    {
        if (!IsLinuxSupported)
        {
            ErrorMessage = UnsupportedMessage;
            return;
        }

        IsInstalling = true;
        ErrorMessage = null;
        StatusMessage = string.Format(Resources.ProtonInstalling, release.DisplayName);
        try
        {
            await _compatibilityToolInstallationService.InstallAsync(release);
            await RefreshInstalledToolsAsync();
            StatusMessage = string.Format(Resources.ProtonInstallComplete, release.DisplayName);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not install Proton release {ReleaseId}.", release.Id);
            ErrorMessage = string.Format(Resources.ProtonInstallError, release.DisplayName, exception.Message);
            StatusMessage = null;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private async Task RefreshInstalledToolsAsync(CancellationToken cancellationToken = default)
    {
        var protonTools = await _compatibilityToolDiscoveryService.DiscoverInstalledToolsAsync(cancellationToken);
        InstalledTools = new ObservableCollection<ProtonToolViewModel>(
            protonTools.Select(protonTool => new ProtonToolViewModel(protonTool)));
    }

    partial void OnInstalledToolsChanged(ObservableCollection<ProtonToolViewModel> value)
    {
        OnPropertyChanged(nameof(HasInstalledTools));
        OnPropertyChanged(nameof(HasNoInstalledTools));
    }

    partial void OnProvidersChanged(ObservableCollection<ProtonReleaseProviderViewModel> value)
    {
        OnPropertyChanged(nameof(HasNoProviders));
        OnPropertyChanged(nameof(NavigationFieldCount));
    }

    partial void OnSelectedProviderChanged(ProtonReleaseProviderViewModel? value)
    {
        OnPropertyChanged(nameof(NavigationFieldCount));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasErrorMessage));
        OnPropertyChanged(nameof(HasNoErrorMessage));
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsInstallingChanged(bool value) => OnPropertyChanged(nameof(CanInteract));
}
