using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels.Settings;

public sealed partial class ProtonReleaseCatalogViewModel : ObservableObject
{
    public ProtonReleaseCatalogViewModel(
        ProtonReleaseCatalog catalog,
        Func<ProtonRelease, Task> installRelease,
        Action<ProtonReleaseCatalogViewModel> selectCatalog,
        bool canInstall)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(installRelease);
        ArgumentNullException.ThrowIfNull(selectCatalog);

        DisplayName = catalog.ProviderName;
        ErrorMessage = catalog.ErrorMessage;
        SelectCommand = new RelayCommand(() => selectCatalog(this));
        Releases = new ObservableCollection<ProtonReleaseOptionViewModel>(
            catalog.Releases.Select(release => new ProtonReleaseOptionViewModel(
                release,
                () => installRelease(release),
                canInstall)));
    }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IBrush SelectionBackground => new SolidColorBrush(Color.Parse(IsSelected ? "#185DA8" : "#2C3740"));

    public IBrush SelectionBorder => new SolidColorBrush(Color.Parse(IsSelected ? "#72D9FF" : "#39454F"));

    public ICommand SelectCommand { get; }

    public string? ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoError => !HasError;

    public bool HasReleases => Releases.Count > 0;

    public bool HasNoReleases => !HasReleases;

    public ObservableCollection<ProtonReleaseOptionViewModel> Releases { get; }

    public string ErrorLabel => string.Format(Resources.ProtonCatalogLoadError, ErrorMessage);

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBackground));
        OnPropertyChanged(nameof(SelectionBorder));
    }
}
