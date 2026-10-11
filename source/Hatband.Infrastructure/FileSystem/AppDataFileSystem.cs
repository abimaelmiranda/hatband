using Hatband.Core.Enums.Host;

namespace Hatband.Infrastructure.FileSystem;

public sealed class AppDataFileSystem : IAppDataFileSystem
{
    private readonly string rootDirectory;
    private readonly StringComparison pathComparison;
    private readonly HostOperatingSystem hostOperatingSystem;

    public AppDataFileSystem(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostSystemInfo.UserProfileDirectory);

        hostOperatingSystem = hostSystemInfo.Platform;
        pathComparison = hostSystemInfo.Platform == HostOperatingSystem.Windows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        rootDirectory = Path.GetFullPath(Path.Combine(hostSystemInfo.UserProfileDirectory, ".hatband"));
        Directory.CreateDirectory(rootDirectory);
        Directory.CreateDirectory(GetPath("tools"));
        Directory.CreateDirectory(GetPath("tools/proton"));
        Directory.CreateDirectory(GetPath("games"));
    }

    public string RootDirectory => rootDirectory;

    public string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("App data paths must be relative.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        var rootPrefix = rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? rootDirectory
            : rootDirectory + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, pathComparison) &&
            !string.Equals(fullPath, rootDirectory, pathComparison))
        {
            throw new ArgumentException("App data paths must stay inside the Hatband data directory.", nameof(relativePath));
        }

        return fullPath;
    }

    public string ResolveGamePrefixPath(Guid gameId, string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        if (!Path.IsPathFullyQualified(installDirectory) || !Directory.Exists(installDirectory))
        {
            throw new DirectoryNotFoundException($"The game installation directory does not exist: {installDirectory}");
        }

        var managedPrefixPath = this.GetGamePrefixPath(gameId);
        if (hostOperatingSystem != HostOperatingSystem.Linux)
        {
            return managedPrefixPath;
        }

        var externalPrefixPath = GetGamePrefixStoragePath(gameId, installDirectory);
        return GamePrefixStorage.Link(managedPrefixPath, externalPrefixPath);
    }

    public string GetGamePrefixStoragePath(Guid gameId, string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        if (!Path.IsPathFullyQualified(installDirectory))
        {
            throw new ArgumentException("The game installation directory must be absolute.", nameof(installDirectory));
        }

        if (hostOperatingSystem != HostOperatingSystem.Linux)
        {
            return this.GetGamePrefixPath(gameId);
        }

        var volumeRoot = LinuxMountPointResolver.FindMountPoint(installDirectory);
        var externalGameDirectory = Path.Combine(volumeRoot, ".hatband", "games", gameId.ToString("D"));
        return Path.Combine(externalGameDirectory, "prefix");
    }

    public bool FileExists(string relativePath) => File.Exists(GetPath(relativePath));

    public Task<byte[]> ReadAllBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        return File.ReadAllBytesAsync(GetPath(relativePath), cancellationToken);
    }

    public async Task WriteAllBytesAtomicallyAsync(
        string relativePath,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetPath(relativePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("An app data file path must include a directory.");
        }

        Directory.CreateDirectory(directoryPath);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, contents.ToArray(), cancellationToken);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void CreateDirectory(string relativePath)
    {
        Directory.CreateDirectory(GetPath(relativePath));
    }
}
