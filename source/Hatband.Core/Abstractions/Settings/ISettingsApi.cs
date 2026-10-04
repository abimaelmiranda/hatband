namespace Hatband.Core.Abstractions.Settings;

/// <summary>
/// Provides typed access to settings sections registered by the application and its extensions.
/// A settings type is available only when its section descriptor is registered.
/// </summary>
public interface ISettingsApi
{
    /// <summary>
    /// Gets the registered root settings sections for presentation or discovery. A UI can present
    /// each section as a navigation item and inspect its DTO by convention: booleans as checkboxes,
    /// enums as choices, numbers as numeric fields, strings as text fields, and nested settings DTOs
    /// as tabs within their section.
    /// </summary>
    IReadOnlyList<ISettingsSection> GetSections();

    /// <summary>
    /// Loads a section by its registered settings DTO type.
    /// </summary>
    Task<TSettings> GetSectionAsync<TSettings>(CancellationToken cancellationToken = default)
        where TSettings : class;

    /// <summary>
    /// Persists only the specified settings DTO while preserving other and currently unregistered
    /// sections. Implementations serialize writes so concurrent updates do not overwrite one another.
    /// </summary>
    Task SaveSectionAsync<TSettings>(
        TSettings settings,
        CancellationToken cancellationToken = default)
        where TSettings : class;
}
