using System.Runtime.Versioning;
using Hatband.Core.Abstractions.Archives;
using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Compatibility;

namespace Hatband.Plugins.Proton.Compatibility;

public sealed class ProtonToolInstallationProvider : ICompatibilityToolInstallationProvider
{
    private const string HatbandRunnersDirectory = "tools/proton";
    private const string DownloadDirectory = "tools/proton/.downloads";
    private const string GitHubReleaseDownloadHost = "github.com";
    private const string ProtonProvisionerId = "proton";
    private static readonly IReadOnlyCollection<string> ProtonReleaseProviderIds =
        Array.AsReadOnly(["proton-ge", "proton-cachyos"]);

    private readonly IAppDataFileSystem _appDataFileSystem;
    private readonly IArchiveExtractionService _archiveExtractionService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IReadOnlyList<ICompatibilityToolLocationProvider> _locationProviders;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProtonToolInstallationProvider(
        IAppDataFileSystem appDataFileSystem,
        IArchiveExtractionService archiveExtractionService,
        IHostSystemInfo hostSystemInfo,
        IEnumerable<ICompatibilityToolLocationProvider> locationProviders,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(archiveExtractionService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(locationProviders);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _appDataFileSystem = appDataFileSystem;
        _archiveExtractionService = archiveExtractionService;
        _hostSystemInfo = hostSystemInfo;
        _locationProviders = locationProviders
            .Where(provider => string.Equals(provider.ProvisionerId, ProtonProvisionerId, StringComparison.Ordinal))
            .ToArray();
        _httpClientFactory = httpClientFactory;
    }

    public string Id => ProtonProvisionerId;

    public IReadOnlyCollection<string> ReleaseProviderIds => ProtonReleaseProviderIds;

    public async Task InstallAsync(CompatibilityToolRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!_hostSystemInfo.IsLinux)
        {
            throw new PlatformNotSupportedException("Proton tools can only be installed on Linux.");
        }

        ValidateRelease(release);

        var targetName = CreateInstallationDirectoryName(release);
        var installationDirectory = await GetInstallationDirectoryAsync(cancellationToken);
        var targetPath = Path.Combine(installationDirectory, targetName);
        var trackingLinkPath = _appDataFileSystem.GetPath(Path.Combine(HatbandRunnersDirectory, targetName));
        var requiresTrackingLink = !PathsEqual(targetPath, trackingLinkPath);
        var isRepair = Directory.Exists(targetPath);
        if (isRepair && !IsRepairableHatbandInstallation(targetPath, trackingLinkPath))
        {
            throw new InvalidOperationException($"'{release.DisplayName}' is already installed in Hatband.");
        }

        _appDataFileSystem.CreateDirectory(DownloadDirectory);
        _appDataFileSystem.CreateDirectory(HatbandRunnersDirectory);
        Directory.CreateDirectory(installationDirectory);
        var archivePath = _appDataFileSystem.GetPath(Path.Combine(DownloadDirectory, $"{Guid.NewGuid():N}-{release.ArchiveFileName}"));
        var stagingRoot = Path.Combine(installationDirectory, ".hatband-proton-staging");
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
                if (requiresTrackingLink)
                {
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

    private async Task<string> GetInstallationDirectoryAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in _locationProviders)
        {
            var locations = await provider.GetLocationsAsync(cancellationToken);
            var destination = locations.FirstOrDefault(location => location.CanInstall);
            if (destination is not null)
            {
                return destination.DirectoryPath;
            }
        }

        return _appDataFileSystem.GetPath(HatbandRunnersDirectory);
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
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

        if (!PathsEqual(trackingLinkPath, targetPath) && !TrackingLinkTargets(trackingLinkPath, targetPath))
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
            if (!PathsEqual(trackingLinkPath, targetPath) && !TrackingLinkTargets(trackingLinkPath, targetPath))
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
