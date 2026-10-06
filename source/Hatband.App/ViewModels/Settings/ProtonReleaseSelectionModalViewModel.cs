using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels.Settings;

public partial class ProtonReleaseSelectionModalViewModel : ModalViewModel<CompatibilityToolRelease>
{
    private readonly ICompatibilityToolReleaseCatalogService _catalogService;
    private CancellationTokenSource? _loadCancellation;

    public ProtonReleaseSelectionModalViewModel(
        CompatibilityToolReleaseProviderInfo provider,
        ICompatibilityToolReleaseCatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(catalogService);
        Provider = provider;
        _catalogService = catalogService;
    }

    public CompatibilityToolReleaseProviderInfo Provider { get; }

    public string Title => string.Format(CultureInfo.CurrentCulture, Resources.ProtonProviderVersionsTitle, Provider.ProviderName);

    [ObservableProperty]
    public partial ObservableCollection<CompatibilityToolRelease> Releases { get; set; } = [];

    [ObservableProperty]
    public partial CompatibilityToolRelease? SelectedRelease { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadedCatalog { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasReleases => Releases.Count > 0;

    public bool HasNoReleases => HasLoadedCatalog && !HasError && !HasReleases;

    public bool CanConfirm => !IsLoading && !HasError && SelectedRelease is not null;

    public string ErrorLabel => string.Format(CultureInfo.CurrentCulture, Resources.ProtonCatalogLoadError, ErrorMessage);

    partial void OnReleasesChanged(ObservableCollection<CompatibilityToolRelease> value)
    {
        OnPropertyChanged(nameof(HasReleases));
        OnPropertyChanged(nameof(HasNoReleases));
    }

    partial void OnSelectedReleaseChanged(CompatibilityToolRelease? value) => OnPropertyChanged(nameof(CanConfirm));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(CanConfirm));

    partial void OnHasLoadedCatalogChanged(bool value) => OnPropertyChanged(nameof(HasNoReleases));

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasNoReleases));
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ErrorLabel));
    }

    [RelayCommand]
    private async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        using var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = loadCancellation;
        IsLoading = true;
        HasLoadedCatalog = false;
        ErrorMessage = null;
        SelectedRelease = null;
        Releases = [];
        try
        {
            var catalog = await _catalogService.GetCatalogAsync(Provider.ProviderId, loadCancellation.Token);
            Releases = new ObservableCollection<CompatibilityToolRelease>(catalog.Releases);
            SelectedRelease = Releases.FirstOrDefault();
            ErrorMessage = catalog.ErrorMessage;
        }
        catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            HasLoadedCatalog = !loadCancellation.IsCancellationRequested;
            _loadCancellation = null;
        }
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        var release = SelectedRelease;
        if (CanConfirm && release is not null)
        {
            Complete(release);
        }
    }

    [RelayCommand]
    private void CancelSelection()
    {
        CancelLoad();
        Cancel();
    }

    public void CancelLoad() => _loadCancellation?.Cancel();
}
