using System.Net.Http;
using System.Text.Json;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Hatband.Infrastructure.Services;

public sealed class ProtonToolManager : IProtonToolManager
{
    private const string HatbandRunnersDirectory = "proton/runners";
    private const string DownloadDirectory = "proton/.downloads";
    private const string StagingDirectory = "proton/.staging";
    private const string GitHubReleaseDownloadHost = "github.com";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly HttpClient httpClient;
    private readonly IReadOnlyList<IProtonReleaseProvider> releaseProviders;

    public ProtonToolManager(
        IAppDataFileSystem appDataFileSystem,
        IHostSystemInfo hostSystemInfo,
        HttpClient httpClient,
        IEnumerable<IProtonReleaseProvider> releaseProviders)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(releaseProviders);

        this.appDataFileSystem = appDataFileSystem;
        this.hostSystemInfo = hostSystemInfo;
        this.httpClient = httpClient;
        this.releaseProviders = releaseProviders.OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        EnsureReleaseProviderIdsAreUnique(this.releaseProviders);
    }

    public Task<IReadOnlyList<ProtonTool>> DiscoverInstalledToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLinuxHost();

        var protonTools = new List<ProtonTool>();
        AddToolsFromDirectory(
            appDataFileSystem.GetPath(HatbandRunnersDirectory),
            ProtonToolSource.Hatband,
            protonTools);

        foreach (var steamDirectory in GetSteamDirectories())
        {
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "steamapps", "common"),
                ProtonToolSource.Steam,
                protonTools);
            AddToolsFromDirectory(
                Path.Combine(steamDirectory, "compatibilitytools.d"),
                ProtonToolSource.Steam,
                protonTools);
        }

        var installedTools = protonTools
            .DistinctBy(tool => tool.InstallationPath, StringComparer.Ordinal)
            .OrderBy(tool => tool.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ProtonTool>>(installedTools);
    }

    public async Task<IReadOnlyList<ProtonReleaseCatalog>> GetCatalogsAsync(
        CancellationToken cancellationToken = default)
    {
        var catalogs = new List<ProtonReleaseCatalog>(releaseProviders.Count);

        foreach (var provider in releaseProviders)
        {
            try
            {
                var releases = await provider.GetLatestReleasesAsync(cancellationToken);
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, releases, null));
            }
            catch (HttpRequestException exception)
            {
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
            catch (JsonException exception)
            {
                catalogs.Add(new ProtonReleaseCatalog(provider.Id, provider.DisplayName, [], exception.Message));
            }
        }

        return catalogs;
    }

    public async Task InstallAsync(ProtonRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        EnsureLinuxHost();
        ValidateRelease(release);

        var targetName = CreateInstallationDirectoryName(release);
        var targetPath = appDataFileSystem.GetPath(Path.Combine(HatbandRunnersDirectory, targetName));
        if (Directory.Exists(targetPath))
        {
            throw new InvalidOperationException($"'{release.DisplayName}' is already installed in Hatband.");
        }

        appDataFileSystem.CreateDirectory(DownloadDirectory);
        appDataFileSystem.CreateDirectory(StagingDirectory);
        var archivePath = appDataFileSystem.GetPath(Path.Combine(DownloadDirectory, $"{Guid.NewGuid():N}-{release.ArchiveFileName}"));
        var extractionPath = appDataFileSystem.GetPath(Path.Combine(StagingDirectory, Guid.NewGuid().ToString("N")));

        try
        {
            await DownloadArchiveAsync(release.DownloadUrl, archivePath, cancellationToken);
            Directory.CreateDirectory(extractionPath);
            await ExtractArchiveAsync(archivePath, extractionPath, cancellationToken);

            var protonDirectory = FindProtonDirectory(extractionPath);
            Directory.Move(protonDirectory, targetPath);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, recursive: true);
            }
        }
    }

    private IEnumerable<string> GetSteamDirectories()
    {
        var homeDirectory = hostSystemInfo.UserProfileDirectory;
        var candidates = new[]
        {
            Path.Combine(homeDirectory, ".steam", "root"),
            Path.Combine(homeDirectory, ".steam", "steam"),
            Path.Combine(homeDirectory, ".local", "share", "Steam"),
            Path.Combine(homeDirectory, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam")
        };

        var steamDirectories = candidates
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var steamDirectory in steamDirectories.ToArray())
        {
            var libraryFoldersFile = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersFile))
            {
                continue;
            }

            foreach (var line in File.ReadLines(libraryFoldersFile))
            {
                var segments = line.Split('"');
                if (segments.Length < 5 || !string.Equals(segments[1], "path", StringComparison.Ordinal))
                {
                    continue;
                }

                var libraryPath = segments[3].Replace("\\\\", "\\", StringComparison.Ordinal);
                if (Directory.Exists(libraryPath))
                {
                    steamDirectories.Add(Path.GetFullPath(libraryPath));
                }
            }
        }

        return steamDirectories.Distinct(StringComparer.Ordinal);
    }

    private static void AddToolsFromDirectory(
        string directoryPath,
        ProtonToolSource source,
        ICollection<ProtonTool> protonTools)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var toolDirectory in Directory.EnumerateDirectories(directoryPath))
        {
            var protonScript = Path.Combine(toolDirectory, "proton");
            if (!File.Exists(protonScript))
            {
                continue;
            }

            var name = Path.GetFileName(toolDirectory);
            protonTools.Add(new ProtonTool(name, name, toolDirectory, source));
        }
    }

    private static void EnsureReleaseProviderIdsAreUnique(IReadOnlyList<IProtonReleaseProvider> providers)
    {
        var duplicateId = providers
            .GroupBy(provider => provider.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"The Proton release provider id '{duplicateId.Key}' is registered more than once.");
        }
    }

    private void EnsureLinuxHost()
    {
        if (hostSystemInfo.Platform != HostOperatingSystem.Linux)
        {
            throw new PlatformNotSupportedException("Proton tools can only be managed on Linux.");
        }
    }

    private static void ValidateRelease(ProtonRelease release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ArchiveFileName);

        if (!string.Equals(Path.GetFileName(release.ArchiveFileName), release.ArchiveFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The Proton release archive name must not contain a path.", nameof(release));
        }

        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(downloadUri.Host, GitHubReleaseDownloadHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Proton release archives must be downloaded from GitHub over HTTPS.", nameof(release));
        }

        if (!release.ArchiveFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) &&
            !release.ArchiveFileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The Proton release archive format is not supported.", nameof(release));
        }
    }

    private static string CreateInstallationDirectoryName(ProtonRelease release)
    {
        var providerId = SanitizePathSegment(release.ProviderId);
        var version = SanitizePathSegment(release.Version);
        var variant = SanitizePathSegment(release.Variant);
        return $"{providerId}-{version}-{variant}";
    }

    private static string SanitizePathSegment(string value)
    {
        return string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-'));
    }

    private async Task DownloadArchiveAsync(
        string downloadUrl,
        string archivePath,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var archiveStream = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            useAsync: true);
        await responseStream.CopyToAsync(archiveStream, cancellationToken);
    }

    private static async Task ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            archivePath,
            cancellationToken: cancellationToken);
        await archive.WriteToDirectoryAsync(
            destinationPath,
            ExtractionOptions.SafeExtract,
            cancellationToken: cancellationToken);
    }

    private static string FindProtonDirectory(string extractionPath)
    {
        var directories = Directory.EnumerateDirectories(extractionPath).ToArray();
        var protonDirectory = directories.Length == 1 && File.Exists(Path.Combine(directories[0], "proton"))
            ? directories[0]
            : extractionPath;
        if (!File.Exists(Path.Combine(protonDirectory, "proton")))
        {
            throw new InvalidDataException("The downloaded archive does not contain a Proton compatibility tool.");
        }

        return protonDirectory;
    }
}
