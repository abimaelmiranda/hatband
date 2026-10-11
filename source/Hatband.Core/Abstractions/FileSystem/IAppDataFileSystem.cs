namespace Hatband.Core.Abstractions.FileSystem;

public interface IAppDataFileSystem
{
    string RootDirectory { get; }

    string GetPath(string relativePath);

    string ResolveGamePrefixPath(Guid gameId, string installDirectory);

    string GetGamePrefixStoragePath(Guid gameId, string installDirectory);

    bool FileExists(string relativePath);

    Task<byte[]> ReadAllBytesAsync(string relativePath, CancellationToken cancellationToken = default);

    Task WriteAllBytesAtomicallyAsync(
        string relativePath,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken = default);

    void CreateDirectory(string relativePath);
}
