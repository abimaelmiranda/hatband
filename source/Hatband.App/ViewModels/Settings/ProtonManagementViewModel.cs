using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Microsoft.Extensions.Logging;

namespace Hatband.App.ViewModels.Settings;

public partial class ProtonManagementViewModel : ObservableObject
{
    private readonly ICompatibilityToolReleaseCatalogService compatibilityToolReleaseCatalogService;
    private readonly ICompatibilityToolDiscoveryService compatibilityToolDiscoveryService;
    private readonly ICompatibilityToolInstallationService compatibilityToolInstallationService;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ILogger<ProtonManagementViewModel> logger;

    [ObservableProperty]
    public partial ObservableCollection<ProtonToolViewModel> InstalledTools { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<ProtonReleaseCatalogViewModel> Catalogs { get; set; } = [];

    [ObservableProperty]
    public partial ProtonReleaseCatalogViewModel? SelectedCatalog { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadedCatalog { get; set; }

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
        ILogger<ProtonManagementViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(compatibilityToolReleaseCatalogService);
        ArgumentNullException.ThrowIfNull(compatibilityToolDiscoveryService);
        ArgumentNullException.ThrowIfNull(compatibilityToolInstallationService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(logger);
        this.compatibilityToolReleaseCatalogService = compatibilityToolReleaseCatalogService;
        this.compatibilityToolDiscoveryService = compatibilityToolDiscoveryService;
        this.compatibilityToolInstallationService = compatibilityToolInstallationService;
        this.hostSystemInfo = hostSystemInfo;
        this.logger = logger;
    }

    public bool IsLinuxSupported => hostSystemInfo.Platform == HostOperatingSystem.Linux;

    public bool CanBrowseCatalogs => IsLinuxSupported;

    public bool IsCatalogBrowsingUnsupported => !CanBrowseCatalogs;

    public bool HasInstalledTools => InstalledTools.Count > 0;

    public bool HasNoInstalledTools => !HasInstalledTools;

    public bool HasNoCatalogs => Catalogs.Count == 0;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoErrorMessage => !HasErrorMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string UnsupportedMessage => Resources.ProtonLinuxOnly;

    public bool CanInteract => !IsInstalling;

    public int NavigationFieldCount => Catalogs.Count + (SelectedCatalog?.Releases.Count ?? 0) + 1;

    [RelayCommand]
    private async Task RefreshAsync()
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
            if (IsLinuxSupported)
            {
                await RefreshInstalledToolsAsync();
            }

            var catalogs = await compatibilityToolReleaseCatalogService.GetCatalogsAsync();
            Catalogs = new ObservableCollection<ProtonReleaseCatalogViewModel>(
                catalogs.Select(catalog => new ProtonReleaseCatalogViewModel(
                    catalog,
                    InstallReleaseAsync,
                    SelectCatalog,
                    IsLinuxSupported)));
            SelectCatalog(Catalogs.FirstOrDefault());
            HasLoadedCatalog = true;
            OnPropertyChanged(nameof(NavigationFieldCount));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not refresh Proton compatibility tools.");
            ErrorMessage = string.Format(Resources.ProtonRefreshError, exception.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectCatalog(ProtonReleaseCatalogViewModel? catalog)
    {
        if (catalog is not null && !Catalogs.Contains(catalog))
        {
            throw new ArgumentException("The Proton catalog does not belong to this view model.", nameof(catalog));
        }

        foreach (var availableCatalog in Catalogs)
        {
            availableCatalog.IsSelected = availableCatalog == catalog;
        }

        SelectedCatalog = catalog;
        OnPropertyChanged(nameof(NavigationFieldCount));
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
            await compatibilityToolInstallationService.InstallAsync(release);
            await RefreshInstalledToolsAsync();
            StatusMessage = string.Format(Resources.ProtonInstallComplete, release.DisplayName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not install Proton release {ReleaseId}.", release.Id);
            ErrorMessage = string.Format(Resources.ProtonInstallError, release.DisplayName, exception.Message);
            StatusMessage = null;
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInteract));
        }
    }

    private async Task RefreshInstalledToolsAsync()
    {
        var protonTools = await compatibilityToolDiscoveryService.DiscoverInstalledToolsAsync();
        InstalledTools = new ObservableCollection<ProtonToolViewModel>(
            protonTools.Select(protonTool => new ProtonToolViewModel(protonTool)));
    }

    partial void OnInstalledToolsChanged(ObservableCollection<ProtonToolViewModel> value)
    {
        OnPropertyChanged(nameof(HasInstalledTools));
        OnPropertyChanged(nameof(HasNoInstalledTools));
    }

    partial void OnCatalogsChanged(ObservableCollection<ProtonReleaseCatalogViewModel> value)
    {
        OnPropertyChanged(nameof(HasNoCatalogs));
    }

    partial void OnSelectedCatalogChanged(ProtonReleaseCatalogViewModel? value)
    {
        OnPropertyChanged(nameof(NavigationFieldCount));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasErrorMessage));
        OnPropertyChanged(nameof(HasNoErrorMessage));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteract));
    }
}
