using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.App.ViewModels.Settings;

public partial class SettingsNavigationViewModel : ObservableObject
{
    private readonly int connectorTabCount;

    public SettingsNavigationViewModel(IEnumerable<ISettingsSection> sections, int connectorTabCount = 0)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentOutOfRangeException.ThrowIfNegative(connectorTabCount);
        this.connectorTabCount = connectorTabCount;
        var discoveredSections = sections
            .Where(section => section.ShowInSettingsNavigation)
            .Select(section => new SettingsSectionOptionViewModel(section));
        Sections = new ObservableCollection<SettingsSectionOptionViewModel>(discoveredSections)
        {
            SettingsSectionOptionViewModel.CreateConnectorsSection(),
            SettingsSectionOptionViewModel.CreateCompatibilitySection()
        };
        if (Sections.Count == 0)
        {
            throw new InvalidOperationException("At least one settings section must be available.");
        }

        SelectedSectionIndex = 0;
        SelectedSection.IsSelected = true;
    }

    [ObservableProperty]
    public partial ObservableCollection<SettingsSectionOptionViewModel> Sections { get; set; }

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsContentActive { get; set; }

    [ObservableProperty]
    public partial int SelectedFieldIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedSettingsTabIndex { get; set; }

    public SettingsSectionOptionViewModel SelectedSection => Sections[SelectedSectionIndex];

    public bool IsCompatibilitySection => SelectedSection.IsCompatibilitySection;

    public bool IsConnectorsSection => SelectedSection.IsConnectorsSection;

    public bool IsSettingsDataSection => !IsCompatibilitySection;

    public int SelectedFieldCount => IsCompatibilitySection
        ? compatibilityFieldCount
        : IsConnectorsSection ? 1 : SelectedSection.GetFieldCount(SelectedSettingsTabIndex);

    private int compatibilityFieldCount = 1;

    public void SetCompatibilityFieldCount(int fieldCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldCount);
        compatibilityFieldCount = Math.Max(fieldCount, 1);
        NotifyFieldCountChanged();
    }

    public void ActivateSection()
    {
        IsContentActive = true;
        SelectedFieldIndex = 0;
    }

    public void DeactivateContent() => IsContentActive = false;

    public void SelectField(int index)
    {
        if (index < 0 || index >= Math.Max(SelectedFieldCount, 1))
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        IsContentActive = true;
        SelectedFieldIndex = index;
    }

    public void SelectSettingsTab(int index)
    {
        var tabCount = IsConnectorsSection
            ? connectorTabCount
            : SelectedSection.EditorDefinition?.Tabs.Count ?? 0;
        if (IsCompatibilitySection || index < 0 || index >= tabCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        SelectedSettingsTabIndex = index;
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

    public IBrush GetFieldBorderBrush(int index) => IsContentActive && SelectedFieldIndex == index
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : Brushes.Transparent;

    partial void OnSelectedSectionIndexChanged(int value)
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            Sections[index].IsSelected = index == value;
        }

        SelectedFieldIndex = 0;
        SelectedSettingsTabIndex = 0;
        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(IsCompatibilitySection));
        OnPropertyChanged(nameof(IsConnectorsSection));
        OnPropertyChanged(nameof(IsSettingsDataSection));
        NotifyFieldCountChanged();
    }

    partial void OnSelectedFieldIndexChanged(int value) => NotifyFieldCountChanged();

    partial void OnSelectedSettingsTabIndexChanged(int value)
    {
        SelectedFieldIndex = 0;
        NotifyFieldCountChanged();
    }

    private void NotifyFieldCountChanged()
    {
        OnPropertyChanged(nameof(SelectedFieldCount));
    }
}
