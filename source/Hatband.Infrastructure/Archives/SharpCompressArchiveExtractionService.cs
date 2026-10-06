using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Readers;
using System.Runtime.Versioning;

namespace Hatband.Infrastructure.Archives;

public sealed class SharpCompressArchiveExtractionService : IArchiveExtractionService
{
    private readonly IHostSystemInfo _hostSystemInfo;

    public SharpCompressArchiveExtractionService(IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        _hostSystemInfo = hostSystemInfo;
    }

    public void ValidateArchiveFileName(string archiveFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFileName);

        if (archiveFileName.Contains(Path.DirectorySeparatorChar) ||
            archiveFileName.Contains(Path.AltDirectorySeparatorChar) ||
            archiveFileName.Contains('\\'))
        {
            throw new ArgumentException("An archive file name must not contain a path.", nameof(archiveFileName));
        }

        if (!IsSupportedArchive(archiveFileName))
        {
            throw new NotSupportedException(
                $"The archive format in '{archiveFileName}' is not supported. Supported formats: .tar, .tar.gz, .tar.xz.");
        }
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ValidateArchiveFileName(Path.GetFileName(archivePath));

        Directory.CreateDirectory(destinationPath);

        await using var reader = await ReaderFactory.OpenAsyncReader(
            archivePath,
            cancellationToken: cancellationToken);
        var options = ExtractionOptions.SafeExtract;
        if (_hostSystemInfo.IsLinux)
        {
            options.SymbolicLinkHandler = (path, target) =>
            {
                File.CreateSymbolicLink(path, target);
            };
        }

        while (await reader.MoveToNextEntryAsync(cancellationToken))
        {
            await reader.WriteEntryToDirectoryAsync(destinationPath, options, cancellationToken);
            if (_hostSystemInfo.IsLinux && reader.Entry is TarEntry tarEntry &&
                !tarEntry.IsDirectory && string.IsNullOrEmpty(tarEntry.LinkTarget))
            {
                RestoreUnixFileMode(destinationPath, tarEntry);
            }
        }
    }

    [SupportedOSPlatform("linux")]
    private static void RestoreUnixFileMode(string destinationPath, TarEntry entry)
    {
        var key = entry.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidDataException("A tar file entry must have a path.");
        }

        var root = Path.GetFullPath(destinationPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A tar file entry must remain inside the extraction directory.");
        }

        const UnixFileMode permissionMask = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        var mode = (UnixFileMode)entry.Mode & permissionMask;
        File.SetUnixFileMode(path, mode);
    }

    private static bool IsSupportedArchive(string archiveFileName)
    {
        return archiveFileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ||
               archiveFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
               archiveFileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase);
    }
}
