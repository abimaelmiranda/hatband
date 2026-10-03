namespace Hatband.App.ViewModels.Settings;

public sealed class SettingsScreenViewModel
{
    public SettingsScreenViewModel(ProtonManagementViewModel protonManagement)
    {
        ArgumentNullException.ThrowIfNull(protonManagement);
        ProtonManagement = protonManagement;
        ProtonManagement.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProtonManagementViewModel.NavigationFieldCount))
            {
                Navigation.SetCompatibilityFieldCount(ProtonManagement.NavigationFieldCount);
            }
        };
    }

    public SettingsNavigationViewModel Navigation { get; } = new();

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
}
