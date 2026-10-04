namespace Hatband.Core.Abstractions.Archives;

public interface IArchiveExtractionService
{
    void ValidateArchiveFileName(string archiveFileName);

    Task ExtractAsync(
        string archivePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
