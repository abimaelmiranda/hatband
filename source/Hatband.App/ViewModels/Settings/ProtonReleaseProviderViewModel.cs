using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Hatband.App.ViewModels.Settings;

public partial class ProtonReleaseProviderViewModel : ObservableObject
{
    public ProtonReleaseProviderViewModel(
        CompatibilityToolReleaseProviderInfo provider,
        Func<ProtonReleaseProviderViewModel, Task> openVersions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(openVersions);
        Provider = provider;
        OpenVersionsCommand = new AsyncRelayCommand(() => openVersions(this));
    }

    public CompatibilityToolReleaseProviderInfo Provider { get; }

    public string ProviderId => Provider.ProviderId;

    public string DisplayName => Provider.ProviderName;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public ICommand OpenVersionsCommand { get; }

    public IBrush SelectionBackground => new SolidColorBrush(Color.Parse(IsSelected ? "#185DA8" : "#2C3740"));

    public IBrush SelectionBorder => new SolidColorBrush(Color.Parse(IsSelected ? "#72D9FF" : "#39454F"));

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBackground));
        OnPropertyChanged(nameof(SelectionBorder));
    }
}
