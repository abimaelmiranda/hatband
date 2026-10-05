using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using Hatband.App.Localization;
using Hatband.Core.Abstractions.Settings;
using Hatband.App.ViewModels.Settings.Fields;

namespace Hatband.App.ViewModels.Settings;

public partial class SettingsSectionOptionViewModel : ObservableObject
{
    public const string GeneralSectionId = "hatband.general";
    public const string CompatibilitySectionId = "hatband.compatibility-tools";
    public const string ConnectorsSectionId = "hatband.connectors";

    public SettingsSectionOptionViewModel(ISettingsSection descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        Id = descriptor.Id;
        Title = descriptor.Id switch
        {
            GeneralSectionId => Resources.General,
            ConnectorsSectionId => Resources.ConnectorsFallback,
            _ => descriptor.DisplayName
        };
        IconGlyph = descriptor.Id switch
        {
            GeneralSectionId => FluentIconGlyph.Settings,
            ConnectorsSectionId => FluentIconGlyph.ArrowSwap,
            _ => FluentIconGlyph.Toolbox
        };
        EditorDefinition = SettingsFieldConvention.CreateEditorDefinition(descriptor.SettingsType);
    }

    private SettingsSectionOptionViewModel(string id, string title, FluentIconGlyph iconGlyph)
    {
        Id = id;
        Title = title;
        IconGlyph = iconGlyph;
        IsCompatibilitySection = true;
    }

    public static SettingsSectionOptionViewModel CreateCompatibilitySection() =>
        new(CompatibilitySectionId, Resources.Compatibility, FluentIconGlyph.Toolbox);

    public bool IsConnectorsSection => Id == ConnectorsSectionId;

    public ISettingsSection? Descriptor { get; }

    public string Id { get; }

    public string Title { get; }

    public FluentIconGlyph IconGlyph { get; }

    public bool IsCompatibilitySection { get; }

    public object? Settings { get; private set; }

    internal SettingsEditorDefinition? EditorDefinition { get; }

    public int GetFieldCount(int selectedTabIndex) => IsCompatibilitySection
        ? 1
        : EditorDefinition?.GetFieldCount(selectedTabIndex) ?? 0;

    public IBrush Background => IsSelected
        ? new SolidColorBrush(Color.Parse("#263640"))
        : Brushes.Transparent;

    public IBrush SelectionBorderBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : Brushes.Transparent;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void SetSettings(object settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Descriptor is null || !Descriptor.SettingsType.IsInstanceOfType(settings))
        {
            throw new ArgumentException("The settings object does not match this section.", nameof(settings));
        }

        Settings = settings;
        OnPropertyChanged(nameof(Settings));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(SelectionBorderBrush));
    }
}
