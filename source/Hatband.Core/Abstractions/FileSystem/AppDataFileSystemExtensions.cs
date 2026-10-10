namespace Hatband.Core.Abstractions.FileSystem;

public static class AppDataFileSystemExtensions
{
    public static string GetGamePrefixPath(this IAppDataFileSystem fileSystem, Guid gameId)
    {
        return fileSystem.GetPath($"games/{gameId:D}/prefix");
    }
}
