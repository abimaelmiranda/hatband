using System.Text;

namespace Hatband.Infrastructure.FileSystem;

internal static class LinuxMountPointResolver
{
    private const string MountInfoPath = "/proc/self/mountinfo";

    public static string FindMountPoint(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string? closestMountPoint = null;
        foreach (var line in File.ReadLines(MountInfoPath))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5)
            {
                continue;
            }

            var mountPoint = DecodeMountInfoPath(fields[4]);
            if (IsWithinMount(fullPath, mountPoint)
                && (closestMountPoint is null || mountPoint.Length > closestMountPoint.Length))
            {
                closestMountPoint = mountPoint;
            }
        }

        return closestMountPoint
            ?? throw new IOException($"Could not find the mounted filesystem containing the game path: {path}");
    }

    private static bool IsWithinMount(string path, string mountPoint)
    {
        var normalizedMountPoint = Path.TrimEndingDirectorySeparator(mountPoint);
        return path.Equals(normalizedMountPoint, StringComparison.Ordinal)
            || normalizedMountPoint == Path.DirectorySeparatorChar.ToString()
            || path.StartsWith(normalizedMountPoint + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string DecodeMountInfoPath(string encodedPath)
    {
        var decodedPath = new StringBuilder(encodedPath.Length);
        for (var index = 0; index < encodedPath.Length; index++)
        {
            if (encodedPath[index] == '\\' && index + 3 < encodedPath.Length
                && IsOctalDigit(encodedPath[index + 1])
                && IsOctalDigit(encodedPath[index + 2])
                && IsOctalDigit(encodedPath[index + 3]))
            {
                var character = Convert.ToInt32(encodedPath.Substring(index + 1, 3), 8);
                decodedPath.Append((char)character);
                index += 3;
                continue;
            }

            decodedPath.Append(encodedPath[index]);
        }

        return decodedPath.ToString();
    }

    private static bool IsOctalDigit(char value)
    {
        return value is >= '0' and <= '7';
    }
}
