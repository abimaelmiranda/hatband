using Hatband.Core.Abstractions;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Hatband.Infrastructure.Services;

public sealed class SharpCompressArchiveExtractionService : IArchiveExtractionService
{
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
                $"The archive format in '{archiveFileName}' is not supported. Supported formats: .tar.gz, .tar.xz.");
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
        await reader.WriteAllToDirectoryAsync(
            destinationPath,
            ExtractionOptions.SafeExtract,
            cancellationToken: cancellationToken);
    }

    private static bool IsSupportedArchive(string archiveFileName)
    {
        return archiveFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
               archiveFileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase);
    }
}
