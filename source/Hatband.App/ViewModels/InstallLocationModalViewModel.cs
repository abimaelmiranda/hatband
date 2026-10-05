using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.ViewModels;

/// <summary>
/// Captures installation-location choice from a nonempty provider catalog before installation starts.
/// </summary>
public partial class InstallLocationModalViewModel : ModalViewModel<GameInstallLocation>
{
    public InstallLocationModalViewModel(IReadOnlyList<GameInstallLocation> locations)
    {
        if (locations.Count == 0)
        {
            throw new ArgumentException("An installation location picker requires at least one location.", nameof(locations));
        }

        Locations = locations;
        SelectedLocation = locations[0];
    }

    public IReadOnlyList<GameInstallLocation> Locations { get; }

    [ObservableProperty]
    public partial GameInstallLocation SelectedLocation { get; set; }
}
