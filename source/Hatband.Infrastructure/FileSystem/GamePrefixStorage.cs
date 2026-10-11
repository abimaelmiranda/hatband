namespace Hatband.Infrastructure.FileSystem;

internal static class GamePrefixStorage
{
    public static string Link(string managedPrefixPath, string externalPrefixPath)
    {
        var managedParent = Path.GetDirectoryName(managedPrefixPath)
            ?? throw new InvalidOperationException("The managed game prefix has no parent directory.");
        var externalParent = Path.GetDirectoryName(externalPrefixPath)
            ?? throw new InvalidOperationException("The external game prefix has no parent directory.");

        Directory.CreateDirectory(managedParent);
        Directory.CreateDirectory(externalParent);

        if (TryGetDirectoryLinkTarget(managedPrefixPath, out var linkTarget))
        {
            var linkedPrefixPath = Path.GetFullPath(Path.Combine(managedParent, linkTarget));
            if (PathsEqual(linkedPrefixPath, externalPrefixPath))
            {
                Directory.CreateDirectory(externalPrefixPath);
                return managedPrefixPath;
            }

            if (!Directory.Exists(linkedPrefixPath) || Directory.Exists(externalPrefixPath))
            {
                throw new IOException($"The managed game prefix already links to a different location: {managedPrefixPath}");
            }

            CopyPrefix(linkedPrefixPath, externalPrefixPath);
            Directory.Delete(managedPrefixPath);
            try
            {
                Directory.CreateSymbolicLink(managedPrefixPath, externalPrefixPath);
                VerifyLink(managedPrefixPath, externalPrefixPath);
            }
            catch
            {
                if (TryGetDirectoryLinkTarget(managedPrefixPath, out _))
                {
                    Directory.Delete(managedPrefixPath);
                }

                Directory.CreateSymbolicLink(managedPrefixPath, linkedPrefixPath);
                Directory.Delete(externalPrefixPath, recursive: true);
                throw;
            }

            Directory.Delete(linkedPrefixPath, recursive: true);
            return managedPrefixPath;
        }

        var hasManagedPrefix = Directory.Exists(managedPrefixPath);
        var hasExternalPrefix = Directory.Exists(externalPrefixPath);
        if (hasManagedPrefix && hasExternalPrefix)
        {
            throw new IOException("Both the local and game-disk prefixes exist. The data must be reconciled before linking them.");
        }

        if (hasManagedPrefix)
        {
            CopyPrefix(managedPrefixPath, externalPrefixPath);
            var backupPath = $"{managedPrefixPath}.migration-{Guid.NewGuid():N}";
            Directory.Move(managedPrefixPath, backupPath);
            try
            {
                Directory.CreateSymbolicLink(managedPrefixPath, externalPrefixPath);
                VerifyLink(managedPrefixPath, externalPrefixPath);
            }
            catch
            {
                if (TryGetDirectoryLinkTarget(managedPrefixPath, out _))
                {
                    Directory.Delete(managedPrefixPath);
                }

                Directory.Move(backupPath, managedPrefixPath);
                Directory.Delete(externalPrefixPath, recursive: true);
                throw;
            }

            Directory.Delete(backupPath, recursive: true);
            return managedPrefixPath;
        }

        Directory.CreateDirectory(externalPrefixPath);
        try
        {
            Directory.CreateSymbolicLink(managedPrefixPath, externalPrefixPath);
            VerifyLink(managedPrefixPath, externalPrefixPath);
        }
        catch
        {
            if (TryGetDirectoryLinkTarget(managedPrefixPath, out _))
            {
                Directory.Delete(managedPrefixPath);
            }

            throw;
        }

        return managedPrefixPath;
    }

    private static void CopyPrefix(string sourcePath, string destinationPath)
    {
        var stagingPath = $"{destinationPath}.migration-{Guid.NewGuid():N}";
        try
        {
            CopyDirectory(sourcePath, stagingPath);
            VerifyDirectoryCopy(sourcePath, stagingPath);
            Directory.Move(stagingPath, destinationPath);
        }
        catch
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }

            throw;
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        var sourceInfo = new DirectoryInfo(sourcePath);
        if (sourceInfo.LinkTarget is { } directoryLink)
        {
            Directory.CreateSymbolicLink(destinationPath, directoryLink);
            return;
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var sourceEntry in Directory.EnumerateFileSystemEntries(sourcePath))
        {
            var destinationEntry = Path.Combine(destinationPath, Path.GetFileName(sourceEntry));
            var attributes = File.GetAttributes(sourceEntry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                CopySymbolicLink(sourceEntry, destinationEntry, (attributes & FileAttributes.Directory) != 0);
            }
            else if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyDirectory(sourceEntry, destinationEntry);
            }
            else
            {
                File.Copy(sourceEntry, destinationEntry);
            }
        }
    }

    private static void CopySymbolicLink(string sourcePath, string destinationPath, bool isDirectory)
    {
        var sourceInfo = isDirectory
            ? (FileSystemInfo)new DirectoryInfo(sourcePath)
            : new FileInfo(sourcePath);
        var linkTarget = sourceInfo.LinkTarget
            ?? throw new IOException($"Could not read symbolic link target: {sourcePath}");
        if (isDirectory)
        {
            Directory.CreateSymbolicLink(destinationPath, linkTarget);
        }
        else
        {
            File.CreateSymbolicLink(destinationPath, linkTarget);
        }
    }

    private static void VerifyDirectoryCopy(string sourcePath, string destinationPath)
    {
        var sourceEntries = GetEntryManifest(sourcePath);
        var destinationEntries = GetEntryManifest(destinationPath);
        if (!sourceEntries.SequenceEqual(destinationEntries, StringComparer.Ordinal))
        {
            throw new IOException("The game prefix copy did not preserve all files and symbolic links.");
        }
    }

    private static IReadOnlyList<string> GetEntryManifest(string rootPath)
    {
        var entries = new List<string>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, entry);
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                var info = (attributes & FileAttributes.Directory) != 0
                    ? (FileSystemInfo)new DirectoryInfo(entry)
                    : new FileInfo(entry);
                entries.Add($"link:{relativePath}:{info.LinkTarget}");
            }
            else if ((attributes & FileAttributes.Directory) != 0)
            {
                entries.Add($"directory:{relativePath}");
            }
            else
            {
                entries.Add($"file:{relativePath}:{new FileInfo(entry).Length}");
            }
        }

        entries.Sort(StringComparer.Ordinal);
        return entries;
    }

    private static bool TryGetDirectoryLinkTarget(string path, out string target)
    {
        var linkTarget = new DirectoryInfo(path).LinkTarget;
        target = linkTarget ?? string.Empty;
        return linkTarget is not null;
    }

    private static void VerifyLink(string linkPath, string expectedTarget)
    {
        if (!TryGetDirectoryLinkTarget(linkPath, out var linkTarget)
            || !PathsEqual(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(linkPath)!, linkTarget)), expectedTarget))
        {
            throw new IOException($"Could not verify the game prefix symbolic link: {linkPath}");
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.Ordinal);
    }
}
