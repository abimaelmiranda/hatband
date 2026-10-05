using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Hatband.App.ViewModels.Settings.Fields;

internal static class SettingsFieldConvention
{
    private static readonly HashSet<Type> SupportedValueTypes =
    [
        typeof(string),
        typeof(bool),
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    ];

    public static SettingsEditorDefinition CreateEditorDefinition(Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(settingsType);

        var parentTypes = new HashSet<Type> { settingsType };
        var rootFields = new List<SettingsPanelDefinition.Field>();
        var tabs = new List<SettingsEditorDefinition.Tab>();

        foreach (var property in GetEditableProperties(settingsType))
        {
            var propertyPath = new[] { property };
            if (IsSupported(property.PropertyType))
            {
                rootFields.Add(new SettingsPanelDefinition.Field(property, propertyPath));
                continue;
            }

            if (!TryGetNestedSettingsType(property.PropertyType, out var nestedType) || parentTypes.Contains(nestedType))
            {
                continue;
            }

            var nestedPanel = CreateNestedPanel(nestedType, propertyPath, parentTypes);
            if (nestedPanel.FieldCount > 0)
            {
                tabs.Add(new SettingsEditorDefinition.Tab(property, nestedPanel));
            }
        }

        if (tabs.Count == 0)
        {
            return new SettingsEditorDefinition(new SettingsPanelDefinition(rootFields), []);
        }

        if (rootFields.Count > 0)
        {
            tabs.Insert(0, new SettingsEditorDefinition.Tab(null, new SettingsPanelDefinition(rootFields)));
        }

        return new SettingsEditorDefinition(null, tabs);
    }

    public static bool IsSupported(Type propertyType)
    {
        var type = GetValueType(propertyType);
        return type.IsEnum || SupportedValueTypes.Contains(type);
    }

    public static string GetDisplayName(PropertyInfo property)
    {
        var displayName = property.GetCustomAttribute<DisplayAttribute>()?.GetName();
        return string.IsNullOrWhiteSpace(displayName) ? property.Name : displayName;
    }

    public static Type GetValueType(Type propertyType) => Nullable.GetUnderlyingType(propertyType) ?? propertyType;

    public static bool TryGetNestedSettingsType(Type propertyType, out Type nestedType)
    {
        nestedType = GetValueType(propertyType);
        return nestedType.IsClass && nestedType != typeof(string) &&
               !typeof(System.Collections.IEnumerable).IsAssignableFrom(nestedType) &&
               nestedType.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static SettingsPanelDefinition CreateNestedPanel(
        Type settingsType,
        IReadOnlyList<PropertyInfo> parentPath,
        IReadOnlySet<Type> parentTypes)
    {
        var currentTypes = new HashSet<Type>(parentTypes) { settingsType };
        var blocks = new List<SettingsPanelDefinition.Block>();
        foreach (var property in GetEditableProperties(settingsType))
        {
            var propertyPath = parentPath.Append(property).ToArray();
            if (IsSupported(property.PropertyType))
            {
                blocks.Add(new SettingsPanelDefinition.Field(property, propertyPath));
                continue;
            }

            if (!TryGetNestedSettingsType(property.PropertyType, out var nestedType) || currentTypes.Contains(nestedType))
            {
                continue;
            }

            var nestedPanel = CreateNestedPanel(nestedType, propertyPath, currentTypes);
            if (nestedPanel.FieldCount > 0)
            {
                blocks.Add(new SettingsPanelDefinition.Group(property, propertyPath, nestedPanel));
            }
        }

        return new SettingsPanelDefinition(blocks);
    }

    private static IEnumerable<PropertyInfo> GetEditableProperties(Type settingsType) => settingsType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.CanRead && property.CanWrite)
        .OrderBy(property => property.MetadataToken);
}
