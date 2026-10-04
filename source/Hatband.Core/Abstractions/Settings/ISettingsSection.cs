using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hatband.Core.Abstractions.Settings;

/// <summary>
/// Describes one root settings section contributed by the application or an extension. Its ID is
/// the section key in the settings JSON and can be used as the section's navigation identity in the
/// UI. Section IDs and settings types must each be unique among the registered sections.
/// </summary>
public interface ISettingsSection
{
    /// <summary>
    /// Stable identifier used as the section key in persisted settings.
    /// </summary>
    string Id { get; }

    string DisplayName { get; }

    string? Description { get; }

    /// <summary>
    /// Current schema version for this section.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Settings DTO type. A UI can inspect its properties and use their types and annotations
    /// to choose controls, such as checkboxes for booleans and text fields for strings.
    /// </summary>
    Type SettingsType { get; }

    JsonTypeInfo JsonTypeInfo { get; }

    object CreateDefaultSettings();

    /// <summary>
    /// Converts JSON saved by an earlier schema version to the current version.
    /// </summary>
    JsonElement Migrate(JsonElement settings, int storedVersion);
}

/// <summary>
/// Describes a settings section and its strongly typed settings object.
/// </summary>
public interface ISettingsSection<TSettings> : ISettingsSection
    where TSettings : class
{
    new JsonTypeInfo<TSettings> JsonTypeInfo { get; }

    new TSettings CreateDefaultSettings();
}
