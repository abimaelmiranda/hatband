using Hatband.Integrations.Steam.Abstractions;
using Hatband.Integrations.Steam.Models;
using SteamKit2;

namespace Hatband.Integrations.Steam;

public sealed class SteamInstalledGameScanner : ISteamInstalledGameScanner
{
    private readonly ISteamInstallationService steamInstallationService;

    public SteamInstalledGameScanner(ISteamInstallationService steamInstallationService)
    {
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        this.steamInstallationService = steamInstallationService;
    }

    public async Task<IReadOnlyList<SteamLibraryGame>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        var gamesByAppId = new Dictionary<uint, SteamLibraryGame>();

        foreach (var library in installations.SelectMany(installation => installation.Libraries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamAppsPath = Path.Combine(library.Path, "steamapps");
            if (!Directory.Exists(steamAppsPath))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var game = ReadInstalledGame(manifestPath);
                    if (game is not null)
                    {
                        gamesByAppId.TryAdd(game.AppId, game);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
                {
                    // Steam can leave partially written manifests; continue with other games.
                }
            }
        }

        return gamesByAppId.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static SteamLibraryGame? ReadInstalledGame(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        var manifest = new KeyValue();
        manifest.ReadAsText(stream);
        var appState = FindChild(manifest, "AppState") ?? manifest;
        var stateFlags = GetValue(appState, "StateFlags");
        if (!int.TryParse(stateFlags, out var flags) || (flags & 4) == 0)
        {
            return null;
        }

        var appId = GetValue(appState, "appid") ?? GetValue(appState, "appID");
        var name = GetValue(appState, "name") ?? GetValue(FindChild(appState, "UserConfig"), "name");
        var installDirectoryName = GetValue(appState, "installdir");
        var steamAppsDirectory = Path.GetDirectoryName(manifestPath);
        if (!uint.TryParse(appId, out var parsedAppId) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(installDirectoryName) || steamAppsDirectory is null)
        {
            return null;
        }

        var installDirectory = Path.Combine(steamAppsDirectory, "common", installDirectoryName);
        if (!Directory.Exists(installDirectory))
        {
            installDirectory = Path.Combine(steamAppsDirectory, "music", installDirectoryName);
            if (!Directory.Exists(installDirectory))
            {
                return null;
            }
        }

        return new SteamLibraryGame
        {
            AppId = parsedAppId,
            Name = name.Trim(),
            IsInstalled = true,
            InstallDirectory = installDirectory
        };
    }

    private static KeyValue? FindChild(KeyValue? parent, string name)
    {
        return parent?.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetValue(KeyValue? parent, string name)
    {
        var value = FindChild(parent, name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
