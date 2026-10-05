using System.Reflection;

namespace Hatband.App.ViewModels.Settings.Fields;

internal sealed class SettingsEditorDefinition
{
    public SettingsEditorDefinition(
        SettingsPanelDefinition? singlePanel,
        IReadOnlyList<Tab> tabs)
    {
        SinglePanel = singlePanel;
        Tabs = tabs;
    }

    public SettingsPanelDefinition? SinglePanel { get; }

    public IReadOnlyList<Tab> Tabs { get; }

    public bool HasTabs => Tabs.Count > 0;

    public int GetFieldCount(int selectedTabIndex) =>
        Tabs.ElementAtOrDefault(selectedTabIndex)?.Content.FieldCount ?? SinglePanel?.FieldCount ?? 0;

    internal sealed record Tab(PropertyInfo? Property, SettingsPanelDefinition Content);
}
