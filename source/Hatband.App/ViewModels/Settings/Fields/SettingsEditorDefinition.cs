using System.Reflection;

namespace Hatband.App.ViewModels.Settings.Fields;

internal sealed class SettingsEditorDefinition(
    SettingsPanelDefinition? singlePanel,
    IReadOnlyList<SettingsEditorDefinition.Tab> tabs)
{
    public SettingsPanelDefinition? SinglePanel { get; } = singlePanel;

    public IReadOnlyList<Tab> Tabs { get; } = tabs;

    public bool HasTabs => Tabs.Count > 0;

    public int GetFieldCount(int selectedTabIndex) =>
        Tabs.ElementAtOrDefault(selectedTabIndex)?.Content.FieldCount ?? SinglePanel?.FieldCount ?? 0;

    internal sealed record Tab(PropertyInfo? Property, SettingsPanelDefinition Content);
}
