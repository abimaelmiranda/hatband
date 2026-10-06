using System.Runtime.Versioning;
using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolInstallationService : ICompatibilityToolInstallationService
{
    private const string HatbandRunnersDirectory = "tools/proton";
    private const string DownloadDirectory = "tools/proton/.downloads";
    private const string GitHubReleaseDownloadHost = "github.com";

    private readonly IAppDataFileSystem _appDataFileSystem;
    private readonly IArchiveExtractionService _archiveExtractionService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly ISteamInstallationService _steamInstallationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public CompatibilityToolInstallationService(
        IAppDataFileSystem appDataFileSystem,
        IArchiveExtractionService archiveExtractionService,
        IHostSystemInfo hostSystemInfo,
        ISteamInstallationService steamInstallationService,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(archiveExtractionService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(steamInstallationService);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _appDataFileSystem = appDataFileSystem;
        _archiveExtractionService = archiveExtractionService;
        _hostSystemInfo = hostSystemInfo;
        _steamInstallationService = steamInstallationService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InstallAsync(CompatibilityToolRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!_hostSystemInfo.IsLinux)
        {
            throw new PlatformNotSupportedException("Proton tools can only be installed on Linux.");
        }

        ValidateRelease(release);

        var targetName = CreateInstallationDirectoryName(release);
        var installations = await _steamInstallationService.GetInstallationsAsync(cancellationToken);
        var steamRoot = installations.FirstOrDefault()?.RootPath
            ?? throw new DirectoryNotFoundException("Could not find a Steam installation for the current user.");
        var steamCompatibilityToolsDirectory = Path.Combine(steamRoot, "compatibilitytools.d");
        var targetPath = Path.Combine(steamCompatibilityToolsDirectory, targetName);
        var trackingLinkPath = _appDataFileSystem.GetPath(Path.Combine(HatbandRunnersDirectory, targetName));
        var isRepair = Directory.Exists(targetPath);
        if (isRepair && !IsRepairableHatbandInstallation(targetPath, trackingLinkPath))
        {
            throw new InvalidOperationException($"'{release.DisplayName}' is already installed in Hatband.");
        }

        _appDataFileSystem.CreateDirectory(DownloadDirectory);
        _appDataFileSystem.CreateDirectory(HatbandRunnersDirectory);
        Directory.CreateDirectory(steamCompatibilityToolsDirectory);
        var archivePath = _appDataFileSystem.GetPath(Path.Combine(DownloadDirectory, $"{Guid.NewGuid():N}-{release.ArchiveFileName}"));
        var stagingRoot = Path.Combine(steamCompatibilityToolsDirectory, ".hatband-proton-staging");
        var extractionPath = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        var backupPath = Path.Combine(stagingRoot, $"{Guid.NewGuid():N}.backup");

        try
        {
            await DownloadArchiveAsync(release.DownloadUrl, archivePath, cancellationToken);
            await _archiveExtractionService.ExtractAsync(archivePath, extractionPath, cancellationToken);

            var protonDirectory = FindProtonDirectory(extractionPath);
            if (!HasUserExecutePermission(Path.Combine(protonDirectory, "proton")))
            {
                throw new InvalidDataException("The extracted Proton script does not have user execute permission.");
            }

            if (isRepair)
            {
                ReplaceBrokenInstallation(protonDirectory, targetPath, trackingLinkPath, backupPath, stagingRoot);
            }
            else
            {
                Directory.Move(protonDirectory, targetPath);
                try
                {
                    Directory.CreateSymbolicLink(trackingLinkPath, targetPath);
                }
                catch
                {
                    Directory.Move(targetPath, protonDirectory);
                    throw;
                }
            }
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

    private void ValidateRelease(CompatibilityToolRelease release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ArchiveFileName);
        _archiveExtractionService.ValidateArchiveFileName(release.ArchiveFileName);

        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(downloadUri.Host, GitHubReleaseDownloadHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Proton release archives must be downloaded from GitHub over HTTPS.", nameof(release));
        }
    }

    private static string CreateInstallationDirectoryName(CompatibilityToolRelease release)
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

    [SupportedOSPlatform("linux")]
    private static bool IsRepairableHatbandInstallation(string targetPath, string trackingLinkPath)
    {
        if (new DirectoryInfo(targetPath).LinkTarget is not null)
        {
            return false;
        }

        if (!TrackingLinkTargets(trackingLinkPath, targetPath))
        {
            return false;
        }

        var protonScript = Path.Combine(targetPath, "proton");
        if (!File.Exists(protonScript))
        {
            return false;
        }

        return !HasUserExecutePermission(protonScript);
    }

    [SupportedOSPlatform("linux")]
    private static bool HasUserExecutePermission(string protonScript)
    {
        var mode = File.GetUnixFileMode(protonScript);
        return (mode & UnixFileMode.UserExecute) != 0;
    }

    private static bool TrackingLinkTargets(string trackingLinkPath, string targetPath)
    {
        var trackingLink = new DirectoryInfo(trackingLinkPath);
        var linkTarget = trackingLink.LinkTarget;
        if (linkTarget is null)
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(linkTarget))
        {
            var linkParent = Path.GetDirectoryName(trackingLinkPath)
                ?? throw new InvalidOperationException("The Proton tracking link must have a parent directory.");
            linkTarget = Path.Combine(linkParent, linkTarget);
        }

        return string.Equals(
            Path.GetFullPath(linkTarget),
            Path.GetFullPath(targetPath),
            StringComparison.Ordinal);
    }

    private static void ReplaceBrokenInstallation(
        string stagedProtonDirectory,
        string targetPath,
        string trackingLinkPath,
        string backupPath,
        string stagingRoot)
    {
        Directory.Move(targetPath, backupPath);
        var failedReplacementPath = Path.Combine(stagingRoot, $"{Guid.NewGuid():N}.failed");
        try
        {
            Directory.Move(stagedProtonDirectory, targetPath);
            if (!TrackingLinkTargets(trackingLinkPath, targetPath))
            {
                throw new InvalidOperationException("The Hatband Proton tracking link no longer points to the installation directory.");
            }
        }
        catch (Exception replacementException)
        {
            try
            {
                if (Directory.Exists(targetPath))
                {
                    Directory.Move(targetPath, failedReplacementPath);
                }

                Directory.Move(backupPath, targetPath);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    $"Proton repair failed and the previous installation remains available at '{backupPath}'.",
                    replacementException,
                    rollbackException);
            }

            if (Directory.Exists(failedReplacementPath))
            {
                try
                {
                    Directory.Delete(failedReplacementPath, recursive: true);
                }
                catch (Exception cleanupException)
                {
                    throw new IOException(
                        $"Proton repair failed, the previous installation was restored, and the failed replacement remains at '{failedReplacementPath}'.",
                        new AggregateException(replacementException, cleanupException));
                }
            }

            throw;
        }

        Directory.Delete(backupPath, recursive: true);
    }

    private async Task DownloadArchiveAsync(
        string downloadUrl,
        string archivePath,
        CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
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
