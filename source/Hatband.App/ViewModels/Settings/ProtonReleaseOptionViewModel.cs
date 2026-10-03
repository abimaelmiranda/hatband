using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Hatband.Core.Models;

namespace Hatband.App.ViewModels.Settings;

public sealed class ProtonReleaseOptionViewModel
{
    public ProtonReleaseOptionViewModel(ProtonRelease release, Func<Task> installRelease, bool canInstall)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(installRelease);

        DisplayName = release.DisplayName;
        Version = release.Version;
        Variant = release.Variant;
        PublishedAt = release.PublishedAt;
        IsInstallationUnavailable = !canInstall;
        InstallCommand = new AsyncRelayCommand(installRelease, () => canInstall);
    }

    public string DisplayName { get; }

    public string Version { get; }

    public string Variant { get; }

    public string Summary => $"{Version} · {Variant}";

    public bool IsInstallationUnavailable { get; }

    public DateTimeOffset PublishedAt { get; }

    public ICommand InstallCommand { get; }
}
