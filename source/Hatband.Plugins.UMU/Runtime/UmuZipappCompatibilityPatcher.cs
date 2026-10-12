using System.IO.Compression;
using System.Text;

namespace Hatband.Plugins.UMU.Runtime;

internal static class UmuZipappCompatibilityPatcher
{
    private const string ScriptEntryName = "umu/umu_run.py";
    private const string EmptyClientPath = "\"STEAM_COMPAT_CLIENT_INSTALL_PATH\": \"\",";
    private const string HostClientPath = "\"STEAM_COMPAT_CLIENT_INSTALL_PATH\": os.environ.get(\"STEAM_COMPAT_CLIENT_INSTALL_PATH\", \"\"),";

    public static bool ApplySteamClientInstallPathPatch(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var executable = File.ReadAllBytes(executablePath);
        var zipStart = FindZipStart(executable);
        using var sourceStream = new MemoryStream(executable, writable: false);
        using var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        var scriptEntry = sourceArchive.GetEntry(ScriptEntryName)
            ?? throw new InvalidDataException($"The UMU zipapp does not contain '{ScriptEntryName}'.");

        string script;
        using (var entryStream = scriptEntry.Open())
        using (var reader = new StreamReader(entryStream, Encoding.UTF8))
        {
            script = reader.ReadToEnd();
        }

        if (CountOccurrences(script, HostClientPath) == 1)
        {
            return false;
        }

        if (CountOccurrences(script, EmptyClientPath) != 1)
        {
            throw new InvalidDataException("The UMU zipapp does not have the expected Steam client path setting.");
        }

        var patchedScript = script.Replace(EmptyClientPath, HostClientPath, StringComparison.Ordinal);
        var patchedExecutable = RebuildZipapp(executable, zipStart, sourceArchive, patchedScript);
        ReplaceExecutable(executablePath, patchedExecutable);
        return true;
    }

    private static int FindZipStart(byte[] executable)
    {
        if (executable.Length < 3 || executable[0] != '#' || executable[1] != '!')
        {
            throw new InvalidDataException("The UMU executable is not a Python zipapp.");
        }

        var newlineIndex = Array.IndexOf(executable, (byte)'\n');
        if (newlineIndex < 0)
        {
            throw new InvalidDataException("The UMU zipapp has an invalid interpreter line.");
        }

        return newlineIndex + 1;
    }

    private static byte[] RebuildZipapp(
        byte[] executable,
        int zipStart,
        ZipArchive sourceArchive,
        string patchedScript)
    {
        using var outputStream = new MemoryStream();
        outputStream.Write(executable.AsSpan(0, zipStart));
        using (var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sourceEntry in sourceArchive.Entries)
            {
                var outputEntry = outputArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                outputEntry.LastWriteTime = sourceEntry.LastWriteTime;
                outputEntry.ExternalAttributes = sourceEntry.ExternalAttributes;

                using var outputEntryStream = outputEntry.Open();
                if (sourceEntry.FullName == ScriptEntryName)
                {
                    var scriptBytes = Encoding.UTF8.GetBytes(patchedScript);
                    outputEntryStream.Write(scriptBytes);
                    continue;
                }

                using var sourceEntryStream = sourceEntry.Open();
                sourceEntryStream.CopyTo(outputEntryStream);
            }
        }

        return outputStream.ToArray();
    }

    private static void ReplaceExecutable(string executablePath, byte[] patchedExecutable)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The UMU zipapp patcher is only supported on Linux.");
        }

        var directory = Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("The UMU executable must have a parent directory.");
        var temporaryPath = Path.Combine(directory, $"umu-run-{Guid.NewGuid():N}.tmp");

        try
        {
            var mode = File.GetUnixFileMode(executablePath);
            File.WriteAllBytes(temporaryPath, patchedExecutable);
            File.SetUnixFileMode(temporaryPath, mode);
            File.Move(temporaryPath, executablePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var position = 0;

        while ((position = value.IndexOf(searchValue, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += searchValue.Length;
        }

        return count;
    }
}
