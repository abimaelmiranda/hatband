using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.ViewModels.Settings;
using Hatband.App.Views.Components;

namespace Hatband.App.Views.Screens;

public partial class SettingsScreenView : UserControl
{
    public SettingsScreenView()
    {
        InitializeComponent();
    }

    public ComboBox LanguageComboBoxControl => LanguageComboBox;

    public ComboBox TimeZoneComboBoxControl => TimeZoneComboBox;

    public bool IsSectionNavigationFocused => SettingsSectionList.GetVisualDescendants()
        .OfType<Button>()
        .Any(button => button.IsFocused);

    public void FocusSelectedSettingField()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SelectedSettingsFieldIndex == 0)
        {
            if (viewModel.IsGeneralSettingsSection)
            {
                DirectionalFocusNavigator.Focus(LanguageComboBox);
            }
            else
            {
                FocusSelectedCompatibilityField(viewModel.SelectedSettingsFieldIndex);
            }

            return;
        }

        if (viewModel.IsGeneralSettingsSection)
        {
            DirectionalFocusNavigator.Focus(TimeZoneComboBox);
            return;
        }

        FocusSelectedCompatibilityField(viewModel.SelectedSettingsFieldIndex);
    }

    public void FocusCompatibilityRefreshButton()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var protonManagement = viewModel.SettingsScreen.ProtonManagement;
        var refreshButtonIndex = protonManagement.Catalogs.Count +
            (protonManagement.SelectedCatalog?.Releases.Count ?? 0);
        viewModel.SelectSettingsField(refreshButtonIndex);
        FocusSelectedCompatibilityField(refreshButtonIndex);
    }

    public void OpenSelectedComboBox()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SelectedSettingsFieldIndex == 0)
        {
            if (viewModel.IsGeneralSettingsSection)
            {
                LanguageComboBox.IsDropDownOpen = true;
            }

            return;
        }

        if (viewModel.IsGeneralSettingsSection)
        {
            TimeZoneComboBox.IsDropDownOpen = true;
        }
    }

    public void FocusSelectedSection()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var sectionItem = SettingsSectionList.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.SelectedSettingsSection));
        sectionItem?.FocusItem();
    }

    private void OnSettingsSectionClick(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not ConsoleNavigationItemView { DataContext: SettingsSectionOptionViewModel section })
        {
            return;
        }

        viewModel.SelectSettingsSection(section);
    }

    private void OnSettingsSectionFocusEntered(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: SettingsSectionOptionViewModel section })
        {
            viewModel.SelectSettingsSection(section);
        }
    }

    private void OnLanguageSettingsGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectSettingsField(0);
        }
    }

    private void OnTimeZoneSettingsGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectSettingsField(1);
        }
    }

    private void OnProtonRefreshGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.IsCompatibilitySettingsSection)
        {
            var protonManagement = viewModel.SettingsScreen.ProtonManagement;
            viewModel.SelectSettingsField(
                protonManagement.Catalogs.Count + (protonManagement.SelectedCatalog?.Releases.Count ?? 0));
        }
    }

    private void OnProtonCatalogGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Button { DataContext: ProtonReleaseCatalogViewModel catalog })
        {
            return;
        }

        var catalogIndex = viewModel.SettingsScreen.ProtonManagement.Catalogs.IndexOf(catalog);
        if (catalogIndex < 0)
        {
            return;
        }

        viewModel.SelectSettingsField(catalogIndex);
        catalog.SelectCommand.Execute(null);
    }

    private void OnProtonReleaseGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control { DataContext: ProtonReleaseOptionViewModel release })
        {
            return;
        }

        var protonManagement = viewModel.SettingsScreen.ProtonManagement;
        var releaseIndex = protonManagement.SelectedCatalog?.Releases.IndexOf(release) ?? -1;
        if (releaseIndex < 0)
        {
            return;
        }

        viewModel.SelectSettingsField(protonManagement.Catalogs.Count + releaseIndex);
    }

    private void FocusSelectedCompatibilityField(int fieldIndex)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var protonManagement = viewModel.SettingsScreen.ProtonManagement;
        var controls = ProtonManagementPanel.GetVisualDescendants().OfType<Control>();
        var catalogButtons = new Dictionary<ProtonReleaseCatalogViewModel, Button>();
        var releaseControls = new Dictionary<ProtonReleaseOptionViewModel, Control>();

        foreach (var control in controls)
        {
            if (control is Button button &&
                button.DataContext is ProtonReleaseCatalogViewModel catalog &&
                IsFocusable(button))
            {
                catalogButtons.TryAdd(catalog, button);
            }

            if (control.DataContext is ProtonReleaseOptionViewModel release && IsFocusable(control))
            {
                releaseControls.TryAdd(release, control);
            }
        }

        var focusableControls = new List<Control>();
        foreach (var catalog in protonManagement.Catalogs)
        {
            if (catalogButtons.TryGetValue(catalog, out var catalogButton))
            {
                focusableControls.Add(catalogButton);
            }
        }

        if (protonManagement.SelectedCatalog is { } selectedCatalog)
        {
            foreach (var release in selectedCatalog.Releases)
            {
                if (releaseControls.TryGetValue(release, out var releaseControl))
                {
                    focusableControls.Add(releaseControl);
                }
            }
        }

        if (IsFocusable(ProtonRefreshButton))
        {
            focusableControls.Add(ProtonRefreshButton);
        }

        if ((uint)fieldIndex >= (uint)focusableControls.Count)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(focusableControls[fieldIndex]);
    }

    private static bool IsFocusable(Control control) =>
        control.IsVisible && control.IsEnabled && control.Focusable && control.IsTabStop;

    public bool CloseOpenComboBox()
    {
        if (TimeZoneComboBox.IsDropDownOpen)
        {
            TimeZoneComboBox.IsDropDownOpen = false;
            return true;
        }

        if (LanguageComboBox.IsDropDownOpen)
        {
            LanguageComboBox.IsDropDownOpen = false;
            return true;
        }

        return false;
    }

    public bool HasOpenComboBox()
    {
        return LanguageComboBox.IsDropDownOpen || TimeZoneComboBox.IsDropDownOpen;
    }
}
