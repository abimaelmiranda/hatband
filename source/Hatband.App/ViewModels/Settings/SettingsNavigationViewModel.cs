using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.App.Localization;

namespace Hatband.App.ViewModels.Settings;

public partial class SettingsNavigationViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<SettingsSectionOptionViewModel> Sections { get; set; } = CreateSections();

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsContentActive { get; set; }

    [ObservableProperty]
    public partial int SelectedFieldIndex { get; set; }

    public bool IsGeneralSection => SelectedSection.Section == SettingsSection.General;

    public bool IsCompatibilitySection => SelectedSection.Section == SettingsSection.Compatibility;

    public bool IsSectionPlaceholderVisible => !IsGeneralSection && !IsCompatibilitySection;

    private int compatibilityFieldCount = 1;

    public void SetCompatibilityFieldCount(int fieldCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldCount);
        compatibilityFieldCount = Math.Max(fieldCount, 1);
        if (IsCompatibilitySection && SelectedFieldIndex >= compatibilityFieldCount)
        {
            SelectedFieldIndex = compatibilityFieldCount - 1;
        }
    }

    public SettingsSectionOptionViewModel SelectedSection => Sections[SelectedSectionIndex];

    public bool IsLanguageFieldSelected => IsContentActive && IsGeneralSection && SelectedFieldIndex == 0;

    public bool IsTimeZoneFieldSelected => IsContentActive && IsGeneralSection && SelectedFieldIndex == 1;

    public IBrush LanguageFieldBorderBrush => GetFieldBorderBrush(IsLanguageFieldSelected);

    public IBrush TimeZoneFieldBorderBrush => GetFieldBorderBrush(IsTimeZoneFieldSelected);

    public void ActivateSection()
    {
        IsContentActive = true;
        SelectedFieldIndex = 0;
    }

    public void DeactivateContent()
    {
        IsContentActive = false;
    }

    public void SelectField(int index)
    {
        if (index < 0 || index >= GetFieldCount())
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        IsContentActive = true;
        SelectedFieldIndex = index;
    }

    public void SelectSection(SettingsSectionOptionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var index = Sections.IndexOf(section);
        if (index < 0)
        {
            throw new ArgumentException("The settings section does not belong to this navigation view model.", nameof(section));
        }

        IsContentActive = false;
        SelectedSectionIndex = index;
    }

    partial void OnSelectedSectionIndexChanged(int value)
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            Sections[index].IsSelected = index == value;
        }

        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsCompatibilitySection));
        OnPropertyChanged(nameof(IsSectionPlaceholderVisible));
        NotifyFieldSelectionChanged();
    }

    partial void OnIsContentActiveChanged(bool value)
    {
        NotifyFieldSelectionChanged();
    }

    partial void OnSelectedFieldIndexChanged(int value)
    {
        NotifyFieldSelectionChanged();
    }

    private void NotifyFieldSelectionChanged()
    {
        OnPropertyChanged(nameof(IsLanguageFieldSelected));
        OnPropertyChanged(nameof(IsTimeZoneFieldSelected));
        OnPropertyChanged(nameof(LanguageFieldBorderBrush));
        OnPropertyChanged(nameof(TimeZoneFieldBorderBrush));
    }

    private int GetFieldCount()
    {
        if (IsGeneralSection)
        {
            return 2;
        }

        if (IsCompatibilitySection)
        {
            return compatibilityFieldCount;
        }

        return 0;
    }

    private static ObservableCollection<SettingsSectionOptionViewModel> CreateSections()
    {
        var sections = new ObservableCollection<SettingsSectionOptionViewModel>
        {
            new(SettingsSection.General, Resources.General, "◉"),
            new(SettingsSection.Compatibility, Resources.Compatibility, "⌁"),
            new(SettingsSection.Appearance, Resources.Appearance, "◐"),
            new(SettingsSection.Controls, Resources.Controls, "⌘"),
            new(SettingsSection.Library, Resources.Library, "▦")
        };
        sections[0].IsSelected = true;
        return sections;
    }

    private static IBrush GetFieldBorderBrush(bool isSelected)
    {
        return isSelected
            ? new SolidColorBrush(Color.Parse("#72D9FF"))
            : Brushes.Transparent;
    }
}
