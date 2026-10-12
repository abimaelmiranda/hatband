using System.Globalization;
using System.Text;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Games.PostInstall;
using Hatband.Plugins.Steam.Abstractions;
using Hatband.Plugins.Steam.Models;

namespace Hatband.Plugins.Steam.PostInstall;

public sealed class SteamGamePostInstallProvider : IGamePostInstallProvider
{
    private readonly ISteamInstalledGameScanner installedGameScanner;
    private readonly ISteamInstallationService steamInstallationService;

    public SteamGamePostInstallProvider(
        ISteamInstalledGameScanner installedGameScanner,
        ISteamInstallationService steamInstallationService)
    {
        ArgumentNullException.ThrowIfNull(installedGameScanner);
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        this.installedGameScanner = installedGameScanner;
        this.steamInstallationService = steamInstallationService;
    }

    public string SourceId => GameSourceIds.Steam;

    public async Task<GamePostInstallPlan?> GetPlanAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.SourceId != SourceId)
        {
            return null;
        }

        if (game.InstallationInfo is not { } installationInfo)
        {
            throw new InvalidOperationException($"Steam game '{game.Name}' has no local installation information.");
        }

        if (!uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            throw new InvalidOperationException($"Steam game '{game.Name}' does not have a valid Steam app ID.");
        }

        var installedGames = await installedGameScanner.ScanAsync(cancellationToken);
        var installedGame = installedGames.FirstOrDefault(candidate =>
            candidate.AppId == appId &&
            candidate.InstallDirectory is { } candidateDirectory &&
            IsSamePath(candidateDirectory, installationInfo.InstallDirectory));
        if (installedGame is null)
        {
            throw new InvalidOperationException(
                $"Steam reported app {appId} as installed, but its local app manifest could not be matched to '{installationInfo.InstallDirectory}'.");
        }

        if (installedGame.InstallManifestPath is null)
        {
            throw new InvalidOperationException($"Steam app {appId} has no local app manifest path.");
        }

        return await GetPlanAsync(game, installedGame, cancellationToken);
    }

    public async Task<GamePostInstallPlan?> GetPlanAsync(
        Game game,
        SteamLibraryGame installedGame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(installedGame);
        if (game.SourceId != SourceId)
        {
            return null;
        }

        if (!uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) ||
            appId != installedGame.AppId)
        {
            throw new InvalidOperationException($"Steam game '{game.Name}' does not match installed app {installedGame.AppId}.");
        }

        if (installedGame.InstallManifestPath is not { } manifestPath ||
            installedGame.InstallDirectory is not { } installDirectory)
        {
            throw new InvalidOperationException($"Steam app {appId} is missing its local install manifest or directory.");
        }

        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        return await Task.Run(
            () => BuildPlan(appId, manifestPath, installDirectory, installations, cancellationToken),
            cancellationToken);
    }

    private static GamePostInstallPlan? BuildPlan(
        uint appId,
        string manifestPath,
        string installDirectory,
        IReadOnlyList<SteamInstallation> installations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<GamePostInstallDiagnostic>();
        var sourceManifest = SteamInstallScriptReader.ReadManifest(manifestPath, cancellationToken);
        if (sourceManifest is null)
        {
            diagnostics.Add(Diagnostic(
                "SteamAppManifestUnreadable",
                $"Steam app manifest '{manifestPath}' is missing or could not be parsed."));
        }
        else if (!sourceManifest.IsInstalled)
        {
            diagnostics.Add(Diagnostic(
                "SteamAppManifestNotInstalled",
                $"Steam app manifest '{manifestPath}' no longer marks app {appId} as installed."));
        }

        var sourceLibrary = FindContainingLibrary(manifestPath, installations);
        if (sourceLibrary is null)
        {
            diagnostics.Add(Diagnostic(
                "SteamSourceLibraryNotFound",
                $"Steam library for app manifest '{manifestPath}' could not be identified."));
        }

        var scriptSources = new List<SteamInstallScriptSource>();
        var ownerManifestPathsRead = new HashSet<string>(StringComparer.Ordinal);
        if (sourceManifest is { IsInstalled: true })
        {
            AddOwnScripts(appId, installDirectory, manifestPath, sourceManifest, scriptSources, diagnostics, cancellationToken);
            AddSharedScripts(
                sourceManifest,
                sourceLibrary,
                installations,
                scriptSources,
                ownerManifestPathsRead,
                diagnostics,
                cancellationToken);
        }

        if (sourceManifest is not null && scriptSources.Count == 0 && diagnostics.Count == 0)
        {
            return null;
        }

        var steps = new List<GamePostInstallStep>();
        var revisionParts = new List<(string Path, byte[] Content)>();
        revisionParts.Add((
            manifestPath,
            sourceManifest is null
                ? Encoding.UTF8.GetBytes("manifest-unavailable")
                : SteamInstallScriptReader.GetManifestRevisionBytes(sourceManifest)));

        foreach (var scriptSource in scriptSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(scriptSource.ScriptPath))
            {
                revisionParts.Add((scriptSource.ScriptPath, Encoding.UTF8.GetBytes("script-missing")));
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptMissing",
                    $"Referenced Steam install script '{scriptSource.ScriptPath}' for depot {scriptSource.DepotId} was not found."));
                continue;
            }

            var content = TryReadFileContent(
                scriptSource.ScriptPath,
                "SteamInstallScriptUnreadable",
                diagnostics);
            if (content is null)
            {
                revisionParts.Add((scriptSource.ScriptPath, Encoding.UTF8.GetBytes("script-unreadable")));
                continue;
            }

            revisionParts.Add((scriptSource.ScriptPath, content));
            var parsed = SteamInstallScriptReader.ReadScript(
                scriptSource,
                content,
                appId,
                0,
                cancellationToken);
            steps.AddRange(parsed.Steps);
            diagnostics.AddRange(parsed.Diagnostics);
        }

        if (sourceManifest is not null)
        {
            foreach (var ownerManifestPath in ownerManifestPathsRead.OrderBy(path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ownerManifest = SteamInstallScriptReader.ReadManifest(ownerManifestPath, cancellationToken);
                if (ownerManifest is not null)
                {
                    revisionParts.Add((
                        ownerManifestPath,
                        SteamInstallScriptReader.GetManifestRevisionBytes(ownerManifest)));
                }
                else
                {
                    diagnostics.Add(Diagnostic(
                        "SteamSharedDepotOwnerManifestUnreadable",
                        $"Steam shared depot owner manifest '{ownerManifestPath}' could not be parsed."));
                }
            }
        }

        return new GamePostInstallPlan
        {
            Revision = SteamInstallScriptReader.CreateRevision(revisionParts, diagnostics),
            Steps = steps,
            Diagnostics = diagnostics
        };
    }

    private static void AddOwnScripts(
        uint appId,
        string installDirectory,
        string manifestPath,
        SteamAppManifest manifest,
        ICollection<SteamInstallScriptSource> scriptSources,
        ICollection<GamePostInstallDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var scriptEntry in manifest.InstallScripts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var depotId = scriptEntry.Name;
            if (scriptEntry.Value is not { Length: > 0 } scriptRelativePath)
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptPathMissing",
                    $"App {appId} references an install script for depot {depotId}, but its relative path is missing."));
                continue;
            }

            if (!manifest.InstalledDepots.Contains(depotId, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptDepotNotInstalled",
                    $"App {appId} references install script '{scriptRelativePath}' for depot {depotId}, but that depot is not listed as installed."));
                continue;
            }

            var scriptPath = ResolveScriptPath(installDirectory, scriptRelativePath);
            if (scriptPath is null)
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptPathInvalid",
                    $"App {appId} has an invalid install script path '{scriptRelativePath}' for depot {depotId}."));
                continue;
            }

            var ownerAppId = appId.ToString(CultureInfo.InvariantCulture);
            scriptSources.Add(new SteamInstallScriptSource(
                ownerAppId,
                depotId,
                manifestPath,
                installDirectory,
                scriptRelativePath,
                scriptPath,
                manifest.Language,
                GetScriptOccurrence(scriptSources, ownerAppId, depotId, scriptPath)));
        }
    }

    private static void AddSharedScripts(
        SteamAppManifest gameManifest,
        SteamLibraryLocation? sourceLibrary,
        IReadOnlyList<SteamInstallation> installations,
        ICollection<SteamInstallScriptSource> scriptSources,
        ISet<string> ownerManifestPathsRead,
        ICollection<GamePostInstallDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var depotEntry in gameManifest.SharedDepots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var depotId = depotEntry.Name;
            if (depotEntry.Value is not { Length: > 0 } ownerAppId)
            {
                diagnostics.Add(Diagnostic(
                    "SteamSharedDepotOwnerMissing",
                    $"Shared depot {depotId} has no owner app ID in the game manifest."));
                continue;
            }

            var owner = FindOwnerManifest(ownerAppId, sourceLibrary, installations, cancellationToken);
            if (owner is null)
            {
                diagnostics.Add(Diagnostic(
                    "SteamSharedDepotOwnerNotInstalled",
                    $"Shared depot {depotId} references owner app {ownerAppId}, but no local owner manifest was found."));
                continue;
            }

            ownerManifestPathsRead.Add(owner.ManifestPath);

            if (!owner.Manifest.InstalledDepots.Contains(depotId, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic(
                    "SteamSharedDepotNotInstalled",
                    $"Shared depot {depotId} is not listed as installed in owner app {ownerAppId}'s manifest."));
                continue;
            }

            var matchingScripts = owner.Manifest.InstallScripts
                .Where(pair => string.Equals(pair.Name, depotId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchingScripts.Length == 0)
            {
                continue;
            }

            if (owner.Manifest.InstallDirectoryName is not { } directoryName)
            {
                diagnostics.Add(Diagnostic(
                    "SteamSharedDepotScriptPathInvalid",
                    $"Owner app {ownerAppId} is missing its install directory for shared depot {depotId}."));
                continue;
            }

            var ownerInstallDirectory = Path.Combine(owner.Library.Path, "steamapps", "common", directoryName);
            foreach (var script in matchingScripts)
            {
                if (script.Value is not { Length: > 0 } relativePath)
                {
                    diagnostics.Add(Diagnostic(
                        "SteamInstallScriptPathMissing",
                        $"Owner app {ownerAppId} references an install script for shared depot {depotId}, but its relative path is missing."));
                    continue;
                }

                var scriptPath = ResolveScriptPath(ownerInstallDirectory, relativePath);
                if (scriptPath is null)
                {
                    diagnostics.Add(Diagnostic(
                        "SteamSharedDepotScriptPathInvalid",
                        $"Owner app {ownerAppId} has an invalid install script path '{relativePath}' for shared depot {depotId}."));
                    continue;
                }

                scriptSources.Add(new SteamInstallScriptSource(
                    ownerAppId,
                    depotId,
                    owner.ManifestPath,
                    ownerInstallDirectory,
                    relativePath,
                    scriptPath,
                    gameManifest.Language,
                    GetScriptOccurrence(scriptSources, ownerAppId, depotId, scriptPath)));
            }
        }
    }

    private static OwnerManifest? FindOwnerManifest(
        string ownerAppId,
        SteamLibraryLocation? sourceLibrary,
        IReadOnlyList<SteamInstallation> installations,
        CancellationToken cancellationToken)
    {
        var orderedLibraries = installations
            .SelectMany((installation, installationIndex) => installation.Libraries.Select(library =>
                (InstallationIndex: installationIndex, Library: library)))
            .OrderBy(item => sourceLibrary is not null && IsSamePath(item.Library.Path, sourceLibrary.Path) ? 0 : 1)
            .ThenBy(item => item.InstallationIndex);

        foreach (var item in orderedLibraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(item.Library.Path, "steamapps", $"appmanifest_{ownerAppId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = SteamInstallScriptReader.ReadManifest(manifestPath, cancellationToken);
            if (manifest is { IsInstalled: true })
            {
                return new OwnerManifest(item.Library, manifestPath, manifest);
            }
        }

        return null;
    }

    private static SteamLibraryLocation? FindContainingLibrary(
        string manifestPath,
        IReadOnlyList<SteamInstallation> installations)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        return installations
            .SelectMany(installation => installation.Libraries)
            .FirstOrDefault(library => IsSamePath(
                Path.GetFullPath(Path.Combine(library.Path, "steamapps", Path.GetFileName(manifestPath))),
                fullManifestPath));
    }

    private static string? ResolveScriptPath(
        string installDirectory,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var normalizedRelativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(installDirectory, normalizedRelativePath));
        if (!IsSameOrChildPath(candidatePath, Path.GetFullPath(installDirectory)))
        {
            return null;
        }

        return candidatePath;
    }

    private static int GetScriptOccurrence(
        IEnumerable<SteamInstallScriptSource> scriptSources,
        string ownerAppId,
        string depotId,
        string scriptPath)
    {
        return scriptSources.Count(source =>
            string.Equals(source.OwnerAppId, ownerAppId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(source.DepotId, depotId, StringComparison.OrdinalIgnoreCase) &&
            IsSamePath(source.ScriptPath, scriptPath));
    }

    private static string? ResolveScriptPath(
        string libraryPath,
        string installDirectoryName,
        string relativePath)
    {
        var installDirectory = Path.Combine(libraryPath, "steamapps", "common", installDirectoryName);
        return ResolveScriptPath(installDirectory, relativePath);
    }

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        var relativePath = Path.GetRelativePath(parent, candidate);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) && relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", comparison));
    }

    private static bool IsSamePath(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    private static GamePostInstallDiagnostic Diagnostic(string code, string message)
    {
        return new GamePostInstallDiagnostic { Code = code, Message = message };
    }

    private static byte[]? TryReadFileContent(
        string path,
        string diagnosticCode,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(Diagnostic(
                diagnosticCode,
                $"Steam post-install source file '{path}' could not be read."));
            return null;
        }
    }

    private sealed record OwnerManifest(
        SteamLibraryLocation Library,
        string ManifestPath,
        SteamAppManifest Manifest);
}

internal sealed record SteamInstallScriptSource(
    string OwnerAppId,
    string DepotId,
    string OwnerManifestPath,
    string InstallDirectory,
    string RelativeScriptPath,
    string ScriptPath,
    string Language,
    int ScriptOrdinal);
