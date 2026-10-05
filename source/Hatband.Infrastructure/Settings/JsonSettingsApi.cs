using System.Text.Json;
using System.Text.Json.Nodes;
using Hatband.Core.Abstractions.Settings;
using Hatband.Integrations.Settings;

namespace Hatband.Infrastructure.Settings;

public sealed class JsonSettingsApi : ISettingsApi
{
    private const string SettingsFile = "config.json";
    private readonly IAppDataFileSystem fileSystem;
    private readonly IReadOnlyList<ISettingsSection> sections;
    private readonly Dictionary<Type, ISettingsSection> sectionsByType;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonSettingsApi(IAppDataFileSystem fileSystem, IEnumerable<ISettingsSection> sections)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(sections);

        this.fileSystem = fileSystem;
        this.sections = sections.OrderBy(section => section.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        sectionsByType = this.sections.ToDictionary(section => section.SettingsType);

        if (this.sections.Select(section => section.Id).Distinct(StringComparer.Ordinal).Count() != this.sections.Count)
        {
            throw new InvalidOperationException("Settings section IDs must be unique.");
        }
    }

    public IReadOnlyList<ISettingsSection> GetSections() => sections;

    public async Task<TSettings> GetSectionAsync<TSettings>(CancellationToken cancellationToken = default)
        where TSettings : class
    {
        var section = GetDescriptor<TSettings>();
        return (TSettings)await GetSectionAsync(section, cancellationToken);
    }

    public async Task<object> GetSectionAsync(
        ISettingsSection section,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(section);
        section = GetRegisteredSection(section);
        var root = await ReadDocumentAsync(cancellationToken);

        var sectionData = FindSectionData(root, section);
        if (sectionData is null)
        {
            return section.CreateDefaultSettings();
        }

        using var document = JsonDocument.Parse(sectionData.Settings.ToJsonString());
        var settingsElement = document.RootElement.Clone();

        if (sectionData.Version < section.Version)
        {
            settingsElement = section.Migrate(settingsElement, sectionData.Version);
        }

        return JsonSerializer.Deserialize(settingsElement, section.JsonTypeInfo)
            ?? throw new InvalidDataException($"Settings section '{section.Id}' could not be deserialized.");
    }

    public async Task SaveSectionAsync<TSettings>(TSettings settings, CancellationToken cancellationToken = default)
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(settings);
        var section = GetDescriptor<TSettings>();
        await SaveSectionAsync(section, settings, cancellationToken);
    }

    public async Task SaveSectionAsync(
        ISettingsSection section,
        object settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(settings);
        section = GetRegisteredSection(section);
        if (!section.SettingsType.IsInstanceOfType(settings))
        {
            throw new ArgumentException(
                $"Settings object must be an instance of '{section.SettingsType.FullName}'.",
                nameof(settings));
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadDocumentAsync(cancellationToken);
            var legacyKey = GetLegacyKey(section.SettingsType);
            if (legacyKey is not null)
            {
                root.Remove(legacyKey);
            }

            if (section.SettingsType == typeof(ConnectorsSettings))
            {
                root.Remove("hatband.steam");
                root.Remove("steam");
            }

            root[section.Id] = new JsonObject
            {
                ["Version"] = section.Version,
                ["Settings"] = JsonSerializer.SerializeToNode(settings, section.JsonTypeInfo)
            };
            var contents = JsonSerializer.SerializeToUtf8Bytes(root, new JsonSerializerOptions { WriteIndented = true });
            await fileSystem.WriteAllBytesAtomicallyAsync(SettingsFile, contents, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private ISettingsSection<TSettings> GetDescriptor<TSettings>() where TSettings : class
    {
        if (!sectionsByType.TryGetValue(typeof(TSettings), out var section) || section is not ISettingsSection<TSettings> typedSection)
        {
            throw new InvalidOperationException($"No settings section is registered for '{typeof(TSettings).FullName}'.");
        }

        return typedSection;
    }

    private ISettingsSection GetRegisteredSection(ISettingsSection section)
    {
        if (!sectionsByType.TryGetValue(section.SettingsType, out var registeredSection) ||
            !ReferenceEquals(registeredSection, section))
        {
            throw new InvalidOperationException($"Settings section '{section.Id}' is not registered with this API.");
        }

        return registeredSection;
    }

    private async Task<JsonObject> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!fileSystem.FileExists(SettingsFile))
        {
            return new JsonObject();
        }

        var contents = await fileSystem.ReadAllBytesAsync(SettingsFile, cancellationToken);
        return JsonNode.Parse(contents) as JsonObject
            ?? throw new InvalidDataException("The settings file must contain a JSON object.");
    }

    private static SectionData? FindSectionData(JsonObject root, ISettingsSection section)
    {
        if (root[section.Id] is JsonObject currentSection)
        {
            if (currentSection["Settings"] is not JsonNode currentSettings)
            {
                return null;
            }

            return new SectionData(currentSettings, GetVersion(currentSection));
        }

        var legacySection = FindLegacySection(root, section.SettingsType);
        if (legacySection is null)
        {
            return null;
        }

        var settings = legacySection["Settings"] ?? legacySection;
        return new SectionData(settings, GetVersion(legacySection));
    }

    private static int GetVersion(JsonObject section) => section["Version"]?.GetValue<int>() ?? 0;

    private static JsonObject? FindLegacySection(JsonObject root, Type settingsType)
    {
        if (settingsType == typeof(ConnectorsSettings) && root["hatband.steam"] is JsonObject steamSection)
        {
            return steamSection;
        }

        var legacyKey = GetLegacyKey(settingsType);
        return legacyKey is null ? null : root[legacyKey] as JsonObject;
    }

    private static string? GetLegacyKey(Type settingsType) => settingsType == typeof(GeneralSettings)
        ? "general"
        : settingsType == typeof(ConnectorsSettings)
            ? "steam"
            : null;

    private sealed record SectionData(JsonNode Settings, int Version);
}
