using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hatband.Core.Abstractions.Settings;

/// <summary>
/// Base implementation for typed settings-section definitions.
/// </summary>
public abstract class SettingsSection<TSettings> : ISettingsSection<TSettings>
    where TSettings : class
{
    protected SettingsSection(string id, string displayName, int version, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "A settings section version must be positive.");
        }

        Id = id;
        DisplayName = displayName;
        Version = version;
        Description = description;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string? Description { get; }

    public int Version { get; }

    public Type SettingsType => typeof(TSettings);

    public abstract JsonTypeInfo<TSettings> JsonTypeInfo { get; }

    JsonTypeInfo ISettingsSection.JsonTypeInfo => JsonTypeInfo;

    public abstract TSettings CreateDefaultSettings();

    object ISettingsSection.CreateDefaultSettings() => CreateDefaultSettings();

    public virtual JsonElement Migrate(JsonElement settings, int storedVersion)
    {
        if (storedVersion != Version)
        {
            throw new InvalidOperationException(
                $"Settings section '{Id}' does not support migration from version {storedVersion} to {Version}.");
        }

        return settings.Clone();
    }
}
