using System.Text.Json;
using Hatband.Core.Abstractions;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.Serialization;

namespace Hatband.Infrastructure.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string settingsFilePath;
    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonSettingsStore(IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);

        this.appDataFileSystem = appDataFileSystem;
        settingsFilePath = "config.json";
    }

    public async Task<HatbandSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            if (!appDataFileSystem.FileExists(settingsFilePath))
            {
                var defaultSettings = new HatbandSettings();
                await SaveFileAsync(defaultSettings, cancellationToken);
                return defaultSettings;
            }

            var fileContents = await appDataFileSystem.ReadAllBytesAsync(settingsFilePath, cancellationToken);
            var settings = JsonSerializer.Deserialize(
                fileContents,
                HatbandJsonSerializerContext.Default.HatbandSettings);

            if (settings is null)
            {
                throw new JsonException($"Settings file '{settingsFilePath}' did not contain a settings object.");
            }

            if (settings.General is null)
            {
                throw new JsonException("The General settings section cannot be null.");
            }

            return settings;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveAsync(
        HatbandSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await fileLock.WaitAsync(cancellationToken);

        try
        {
            await SaveFileAsync(settings, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task SaveFileAsync(
        HatbandSettings settings,
        CancellationToken cancellationToken)
    {
        using var contents = new MemoryStream();
        await JsonSerializer.SerializeAsync(
            contents,
            settings,
            HatbandJsonSerializerContext.Default.HatbandSettings,
            cancellationToken);
        await appDataFileSystem.WriteAllBytesAtomicallyAsync(
            settingsFilePath,
            contents.ToArray(),
            cancellationToken);
    }
}
