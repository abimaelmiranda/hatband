using Hatband.Core.Abstractions;
using Hatband.Core.Enums;

namespace Hatband.Infrastructure.Persistence;

public sealed class AppDataFileSystem : IAppDataFileSystem
{
    private readonly string rootDirectory;
    private readonly StringComparison pathComparison;

    public AppDataFileSystem(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostSystemInfo.UserProfileDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostSystemInfo.LocalApplicationDataDirectory);

        pathComparison = hostSystemInfo.Platform == HostOperatingSystem.Windows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        rootDirectory = Path.GetFullPath(Path.Combine(hostSystemInfo.UserProfileDirectory, ".hatband"));
        Directory.CreateDirectory(rootDirectory);
        MigrateLegacyData(hostSystemInfo.LocalApplicationDataDirectory);
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

    private void MigrateLegacyData(string localApplicationDataDirectory)
    {
        var legacyDirectory = Path.Combine(localApplicationDataDirectory, "Hatband");
        if (!Directory.Exists(legacyDirectory))
        {
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(legacyDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(legacyDirectory, sourcePath);
            var destinationPath = GetPath(relativePath);
            if (File.Exists(destinationPath))
            {
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidOperationException("A migrated app data file path must include a directory.");
            }

            Directory.CreateDirectory(destinationDirectory);
            var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.migrate";
            try
            {
                File.Copy(sourcePath, temporaryPath);
                File.Move(temporaryPath, destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
