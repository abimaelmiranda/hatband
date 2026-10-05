using Hatband.Integrations.Steam.Abstractions;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class CompatibilityToolInstallationService : ICompatibilityToolInstallationService
{
    private const string HatbandRunnersDirectory = "proton/runners";
    private const string DownloadDirectory = "proton/.downloads";
    private const string GitHubReleaseDownloadHost = "github.com";

    private readonly IAppDataFileSystem appDataFileSystem;
    private readonly IArchiveExtractionService archiveExtractionService;
    private readonly IHostSystemInfo hostSystemInfo;
    private readonly ISteamInstallationService steamInstallationService;
    private readonly IHttpClientFactory httpClientFactory;

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
        this.appDataFileSystem = appDataFileSystem;
        this.archiveExtractionService = archiveExtractionService;
        this.hostSystemInfo = hostSystemInfo;
        this.steamInstallationService = steamInstallationService;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task InstallAsync(CompatibilityToolRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        EnsureLinuxHost();
        ValidateRelease(release);

        var targetName = CreateInstallationDirectoryName(release);
        var installations = await steamInstallationService.GetInstallationsAsync(cancellationToken);
        var steamRoot = installations.FirstOrDefault()?.RootPath
            ?? throw new DirectoryNotFoundException("Could not find a Steam installation for the current user.");
        var steamCompatibilityToolsDirectory = Path.Combine(steamRoot, "compatibilitytools.d");
        var targetPath = Path.Combine(steamCompatibilityToolsDirectory, targetName);
        var trackingLinkPath = appDataFileSystem.GetPath(Path.Combine(HatbandRunnersDirectory, targetName));
        if (Directory.Exists(targetPath))
        {
            throw new InvalidOperationException($"'{release.DisplayName}' is already installed in Hatband.");
        }

        appDataFileSystem.CreateDirectory(DownloadDirectory);
        appDataFileSystem.CreateDirectory(HatbandRunnersDirectory);
        Directory.CreateDirectory(steamCompatibilityToolsDirectory);
        var archivePath = appDataFileSystem.GetPath(Path.Combine(DownloadDirectory, $"{Guid.NewGuid():N}-{release.ArchiveFileName}"));
        var extractionPath = Path.Combine(steamRoot, ".hatband-proton-staging", Guid.NewGuid().ToString("N"));

        try
        {
            await DownloadArchiveAsync(release.DownloadUrl, archivePath, cancellationToken);
            await archiveExtractionService.ExtractAsync(archivePath, extractionPath, cancellationToken);

            var protonDirectory = FindProtonDirectory(extractionPath);
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

    private void EnsureLinuxHost()
    {
        if (hostSystemInfo.Platform != HostOperatingSystem.Linux)
        {
            throw new PlatformNotSupportedException("Proton tools can only be installed on Linux.");
        }
    }

    private void ValidateRelease(CompatibilityToolRelease release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(release.ArchiveFileName);
        archiveExtractionService.ValidateArchiveFileName(release.ArchiveFileName);

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

    private async Task DownloadArchiveAsync(
        string downloadUrl,
        string archivePath,
        CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
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
