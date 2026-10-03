namespace Hatband.Core.Abstractions;

public interface IArchiveExtractionService
{
    void ValidateArchiveFileName(string archiveFileName);

    Task ExtractAsync(
        string archivePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
