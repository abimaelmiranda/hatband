using Hatband.Core.Abstractions.Settings;

namespace Hatband.App.ViewModels.Settings;

public sealed class SettingsScreenViewModel
{
    public SettingsScreenViewModel(ISettingsApi settingsApi, ProtonManagementViewModel protonManagement)
    {
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(protonManagement);
        ProtonManagement = protonManagement;
        Navigation = new SettingsNavigationViewModel(settingsApi.GetSections());
        ProtonManagement.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtonManagementViewModel.NavigationFieldCount))
            {
                Navigation.SetCompatibilityFieldCount(ProtonManagement.NavigationFieldCount);
            }
        };
    }

    public SettingsNavigationViewModel Navigation { get; }

    public ProtonManagementViewModel ProtonManagement { get; }

    public void SelectSection(SettingsSectionOptionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        Navigation.SelectSection(section);
    }

    public void ActivateSection()
    {
        Navigation.ActivateSection();
        if (Navigation.IsCompatibilitySection &&
            ProtonManagement.CanBrowseCatalogs &&
            !ProtonManagement.HasLoadedCatalog &&
            !ProtonManagement.IsLoading)
        {
            ProtonManagement.RefreshCommand.Execute(null);
        }
    }

    public void SelectConnectorsSection()
    {
        var section = Navigation.Sections.SingleOrDefault(option => option.IsConnectorsSection)
            ?? throw new InvalidOperationException("The connectors settings section is not registered.");
        Navigation.SelectSection(section);
    }
}
