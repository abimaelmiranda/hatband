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
            LanguageComboBox.Focus();
            return;
        }

        TimeZoneComboBox.Focus();
    }

    public void OpenSelectedComboBox()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SelectedSettingsFieldIndex == 0)
        {
            LanguageComboBox.IsDropDownOpen = true;
            return;
        }

        TimeZoneComboBox.IsDropDownOpen = true;
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
