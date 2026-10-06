using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Hatband.Core.Abstractions.Archives;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Host;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

/// <summary>
/// Locates UMU or installs the pinned Hatband-managed copy on first use.
/// </summary>
public sealed class UmuDependencyService
{
    private const string UmuVersion = "1.4.4";
    private const string UmuArchiveName = "umu-launcher-1.4.4-zipapp.tar";
    private const string UmuArchiveSha256 = "eb590691841f7fad3fc3ad8fd5db4ccb87849fe7948e62b28ece7a4ee48cc851";
    private const string UmuDownloadUrl = "https://github.com/Open-Wine-Components/umu-launcher/releases/download/1.4.4/umu-launcher-1.4.4-zipapp.tar";
    private const string UmuDirectory = "proton/umu";
    private const string DownloadDirectory = "proton/.downloads";

    private readonly IAppDataFileSystem _appDataFileSystem;
    private readonly IArchiveExtractionService _archiveExtractionService;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UmuDependencyService> _logger;
    private readonly SemaphoreSlim _installationLock = new(1, 1);

    public UmuDependencyService(
        IAppDataFileSystem appDataFileSystem,
        IArchiveExtractionService archiveExtractionService,
        IHostSystemInfo hostSystemInfo,
        IHttpClientFactory httpClientFactory,
        ILogger<UmuDependencyService> logger)
    {
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(archiveExtractionService);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _appDataFileSystem = appDataFileSystem;
        _archiveExtractionService = archiveExtractionService;
        _hostSystemInfo = hostSystemInfo;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> ResolveExecutableAsync(CancellationToken cancellationToken = default)
    {
        if (!_hostSystemInfo.IsLinux)
        {
            throw new PlatformNotSupportedException("UMU can only be resolved on Linux.");
        }

        var externalExecutable = FindExternalUmuExecutable();
        if (externalExecutable is not null)
        {
            return externalExecutable;
        }

        await _installationLock.WaitAsync(cancellationToken);
        try
        {
            var privateExecutable = Path.Combine(_appDataFileSystem.GetPath(UmuDirectory), "umu-run");
            if (IsExecutable(privateExecutable))
            {
                await EnsurePythonVersionAsync(cancellationToken);
                return privateExecutable;
            }

            if (File.Exists(privateExecutable))
            {
                throw new InvalidDataException("The installed UMU executable does not have execute permissions.");
            }

            if (Directory.Exists(_appDataFileSystem.GetPath(UmuDirectory)))
            {
                throw new InvalidDataException("The managed UMU installation directory exists but does not contain umu-run.");
            }

            await EnsurePythonVersionAsync(cancellationToken);
            await InstallUmuAsync(cancellationToken);
            if (!IsExecutable(privateExecutable))
            {
                throw new InvalidDataException("The UMU package was installed, but the umu-run executable is missing or not executable.");
            }

            return privateExecutable;
        }
        finally
        {
            _installationLock.Release();
        }
    }

    [SupportedOSPlatform("linux")]
    private string? FindExternalUmuExecutable()
    {
        var candidates = new List<string>();
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            candidates.AddRange(pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, "umu-run")));
        }

        candidates.Add(Path.Combine(_hostSystemInfo.UserProfileDirectory, ".local", "bin", "umu-run"));
        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (!IsExecutable(candidate))
            {
                throw new InvalidOperationException($"The UMU executable at '{candidate}' does not have execute permissions.");
            }

            return Path.GetFullPath(candidate);
        }

        return null;
    }

    private async Task EnsurePythonVersionAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("python3")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')");

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Python 3.10 or newer is required to install UMU automatically.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var versionTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Timed out while checking the Python version required by UMU.");
            }

            var versionText = await versionTask;
            var errorText = await errorTask;
            if (process.ExitCode != 0 || !Version.TryParse(versionText.Trim(), out var version) || version < new Version(3, 10))
            {
                var detail = string.IsNullOrWhiteSpace(errorText) ? string.Empty : $" {errorText.Trim()}";
                throw new InvalidOperationException($"Python 3.10 or newer is required to install UMU automatically.{detail}");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Python 3.10 or newer is required to install UMU automatically.", exception);
        }
    }

    [SupportedOSPlatform("linux")]
    private async Task InstallUmuAsync(CancellationToken cancellationToken)
    {
        _appDataFileSystem.CreateDirectory(DownloadDirectory);
        var archivePath = _appDataFileSystem.GetPath(Path.Combine(DownloadDirectory, $"{Guid.NewGuid():N}-{UmuArchiveName}"));
        var targetDirectory = _appDataFileSystem.GetPath(UmuDirectory);
        var stagingDirectory = _appDataFileSystem.GetPath(Path.Combine("proton", $".umu-staging-{Guid.NewGuid():N}"));

        try
        {
            await DownloadUmuAsync(archivePath, cancellationToken);
            await ValidateArchiveHashAsync(archivePath, cancellationToken);
            await _archiveExtractionService.ExtractAsync(archivePath, stagingDirectory, cancellationToken);

            var executablePath = FindUmuExecutable(stagingDirectory);
            SetExecutablePermissions(executablePath);
            var parentDirectory = Path.GetDirectoryName(targetDirectory)
                ?? throw new InvalidOperationException("The UMU installation directory must have a parent directory.");
            Directory.CreateDirectory(parentDirectory);
            var packageDirectory = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidDataException("The UMU executable must be contained in a package directory.");
            if (Directory.Exists(targetDirectory))
            {
                throw new InvalidDataException("The managed UMU installation directory already exists.");
            }

            Directory.Move(packageDirectory, targetDirectory);
            _logger.LogInformation("Installed UMU {UmuVersion} in {UmuDirectory}.", UmuVersion, targetDirectory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Could not download UMU automatically: {exception.Message}", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"The UMU package could not be verified or extracted: {exception.Message}", exception);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Could not install UMU automatically: {exception.Message}", exception);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private async Task DownloadUmuAsync(string archivePath, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            UmuDownloadUrl,
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

    private static async Task ValidateArchiveHashAsync(string archivePath, CancellationToken cancellationToken)
    {
        await using var archiveStream = File.OpenRead(archivePath);
        var hash = await SHA256.HashDataAsync(archiveStream, cancellationToken);
        var actualHash = Convert.ToHexStringLower(hash);
        if (!string.Equals(actualHash, UmuArchiveSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The downloaded UMU archive failed SHA-256 verification.");
        }
    }

    private static string FindUmuExecutable(string stagingDirectory)
    {
        var matches = Directory.EnumerateFiles(stagingDirectory, "umu-run", SearchOption.AllDirectories).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException("The UMU archive must contain exactly one umu-run executable.");
        }

        return matches[0];
    }

    [SupportedOSPlatform("linux")]
    private static void SetExecutablePermissions(string executablePath)
    {
        var mode = File.GetUnixFileMode(executablePath);
        File.SetUnixFileMode(executablePath, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    [SupportedOSPlatform("linux")]
    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var mode = File.GetUnixFileMode(path);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }

}
