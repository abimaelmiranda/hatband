using System.Text.Json;
using Hatband.Core.Abstractions;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.Serialization;

namespace Hatband.Infrastructure.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string settingsFilePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonSettingsStore(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);

        this.settingsFilePath = settingsFilePath;
    }

    public async Task<HatbandSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(settingsFilePath))
            {
                var defaultSettings = new HatbandSettings();
                await SaveFileAsync(defaultSettings, cancellationToken);
                return defaultSettings;
            }

            await using var settingsFile = File.OpenRead(settingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync(
                settingsFile,
                HatbandJsonSerializerContext.Default.HatbandSettings,
                cancellationToken);

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
        var settingsDirectory = Path.GetDirectoryName(settingsFilePath);
        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            throw new InvalidOperationException("The settings file path must include a directory.");
        }

        Directory.CreateDirectory(settingsDirectory);

        var temporaryFilePath = $"{settingsFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var temporaryFile = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    temporaryFile,
                    settings,
                    HatbandJsonSerializerContext.Default.HatbandSettings,
                    cancellationToken);
                await temporaryFile.FlushAsync(cancellationToken);
            }

            File.Move(temporaryFilePath, settingsFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }
}
