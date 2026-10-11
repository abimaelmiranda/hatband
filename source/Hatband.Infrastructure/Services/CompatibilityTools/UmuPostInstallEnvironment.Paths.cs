using System.Text.RegularExpressions;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

internal sealed partial class UmuPostInstallEnvironment
{
    private const string InstallDirectoryToken = "%INSTALLDIR%";
    private const string UserShellFoldersKey = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders";
    private const string MachineShellFoldersKey = "HKEY_LOCAL_MACHINE\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders";

    private readonly SemaphoreSlim _documentsPathLock = new(1, 1);
    private readonly HashSet<string> _allowedCopyRoots = new(StringComparer.Ordinal);
    private readonly object _copyRootsLock = new();
    private string? _documentsPath;

    private async Task<string> ExpandTemplateAsync(
        string template,
        string installDirectory,
        CancellationToken cancellationToken)
    {
        var result = template;
        if (result.Contains(InstallDirectoryToken, StringComparison.OrdinalIgnoreCase))
        {
            var windowsInstallDirectory = await ConvertNativePathToWindowsAsync(installDirectory, cancellationToken);
            result = result.Replace(InstallDirectoryToken, windowsInstallDirectory, StringComparison.OrdinalIgnoreCase);
        }

        var tokens = KnownTemplateTokenRegex().Matches(result).Cast<Match>().Reverse().ToArray();
        foreach (var tokenMatch in tokens)
        {
            var windowsPath = await ResolveKnownTokenAsync(tokenMatch.Value, installDirectory, cancellationToken);
            result = result[..tokenMatch.Index] + windowsPath + result[(tokenMatch.Index + tokenMatch.Length)..];
        }

        if (UnknownTokenRegex().IsMatch(result))
        {
            throw new InvalidOperationException($"The path template contains an unsupported token: {Limit(template)}");
        }

        return result;
    }

    private async Task<string> ResolveKnownTokenAsync(
        string token,
        string installDirectory,
        CancellationToken cancellationToken)
    {
        if (token.Equals("%USER_MYDOCS%", StringComparison.OrdinalIgnoreCase))
        {
            return await ConvertNativePathToWindowsAsync(await GetUserDocumentsPathAsync(cancellationToken), cancellationToken);
        }

        if (token.Equals("%COMMON_MYDOCS%", StringComparison.OrdinalIgnoreCase))
        {
            var commonDocuments = await GetShellFolderPathAsync(MachineShellFoldersKey, "Common Documents", cancellationToken);
            return await ConvertNativePathToWindowsAsync(commonDocuments, cancellationToken);
        }

        if (token.Equals("%STEAMPATH%", StringComparison.OrdinalIgnoreCase))
        {
            if (!_environmentVariables.TryGetValue("STEAM_COMPAT_CLIENT_INSTALL_PATH", out var steamPath)
                || !Path.IsPathFullyQualified(steamPath))
            {
                throw new InvalidOperationException("The Steam client installation path is not available in the UMU environment.");
            }

            return await ConvertNativePathToWindowsAsync(steamPath, cancellationToken);
        }

        if (token.Equals("%ROOTDRIVE%", StringComparison.OrdinalIgnoreCase))
        {
            var installDirectoryWindows = await ConvertNativePathToWindowsAsync(installDirectory, cancellationToken);
            if (installDirectoryWindows.Length < 2 || installDirectoryWindows[1] != ':')
            {
                throw new InvalidOperationException("The game install directory does not resolve to a Windows drive in the UMU prefix.");
            }

            return installDirectoryWindows[..1];
        }

        var variableName = token[1..^1];
        if (variableName.Equals("APPDATA", StringComparison.OrdinalIgnoreCase)
            || variableName.Equals("LOCALAPPDATA", StringComparison.OrdinalIgnoreCase)
            || variableName.Equals("WINDIR", StringComparison.OrdinalIgnoreCase))
        {
            var windowsPath = await ExpandWindowsEnvironmentVariablesAsync(token, cancellationToken);
            if (!windowsPath.Contains(':'))
            {
                throw new InvalidOperationException($"Windows path token '{token}' is not an absolute path in the UMU prefix.");
            }

            var nativePath = await ConvertWindowsPathToNativeAsync(windowsPath, cancellationToken);
            if (!variableName.Equals("WINDIR", StringComparison.OrdinalIgnoreCase))
            {
                lock (_copyRootsLock)
                {
                    _allowedCopyRoots.Add(Path.GetFullPath(nativePath));
                }
            }

            return windowsPath;
        }

        throw new InvalidOperationException($"The path template contains an unsupported token: {token}");
    }

    private async Task<string> GetUserDocumentsPathAsync(CancellationToken cancellationToken)
    {
        if (_documentsPath is not null)
        {
            return _documentsPath;
        }

        await _documentsPathLock.WaitAsync(cancellationToken);
        try
        {
            if (_documentsPath is not null)
            {
                return _documentsPath;
            }

            _documentsPath = await GetShellFolderPathAsync(UserShellFoldersKey, "Personal", cancellationToken);
            return _documentsPath;
        }
        finally
        {
            _documentsPathLock.Release();
        }
    }

    private async Task<string> ExpandWindowsEnvironmentVariablesAsync(string value, CancellationToken cancellationToken)
    {
        var result = value;
        foreach (Match match in WindowsEnvironmentVariableRegex().Matches(value).Cast<Match>().Reverse())
        {
            var variableName = match.Groups["name"].Value;
            var expanded = await RunWindowsUtilityAsync(
                "cmd.exe",
                ["/d", "/c", $"echo %{variableName}%"],
                _defaultWorkingDirectory,
                cancellationToken);
            EnsureSuccess(expanded, $"Could not expand Windows environment variable '{variableName}' in the compatibility prefix.");
            var variableValue = expanded.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(variableValue) || variableValue.Contains('%'))
            {
                throw new InvalidOperationException($"Windows environment variable '{variableName}' is not available in the compatibility prefix.");
            }

            result = result[..match.Index] + variableValue + result[(match.Index + match.Length)..];
        }

        return result;
    }

    private async Task<string> GetShellFolderPathAsync(string keyPath, string valueName, CancellationToken cancellationToken)
    {
        var entry = await ReadRegistryValueAsync(keyPath, GamePostInstallRegistryView.Registry64, valueName, cancellationToken);
        if (entry is not GamePostInstallStringRegistryEntry shellFolder)
        {
            entry = await ReadRegistryValueAsync(keyPath, GamePostInstallRegistryView.Registry32, valueName, cancellationToken);
            shellFolder = entry as GamePostInstallStringRegistryEntry
                ?? throw new InvalidOperationException($"The Windows folder '{valueName}' is not configured in the prefix registry.");
        }

        var windowsPath = shellFolder.Value;
        if (windowsPath.Contains('%'))
        {
            windowsPath = await ExpandWindowsEnvironmentVariablesAsync(windowsPath, cancellationToken);
        }

        if (windowsPath.Contains('%') || !windowsPath.Contains(':'))
        {
            throw new InvalidOperationException($"The Windows folder '{valueName}' could not be resolved to an absolute path.");
        }

        var nativePath = await ConvertWindowsPathToNativeAsync(windowsPath, cancellationToken);
        lock (_copyRootsLock)
        {
            _allowedCopyRoots.Add(Path.GetFullPath(nativePath));
        }

        return nativePath;
    }

    private async Task<string> ConvertNativePathToWindowsAsync(string nativePath, CancellationToken cancellationToken)
    {
        var result = await RunWindowsUtilityAsync("winepath.exe", ["-w", nativePath], _defaultWorkingDirectory, cancellationToken);
        EnsureSuccess(result, $"Could not convert host path '{nativePath}' to a Windows path in the compatibility prefix.");
        var converted = result.StandardOutput.Trim();
        if (converted.Length == 0)
        {
            throw new InvalidOperationException($"UMU returned an empty Windows path for '{nativePath}'.");
        }

        return converted;
    }

    private async Task<string> ConvertWindowsPathToNativeAsync(string windowsPath, CancellationToken cancellationToken)
    {
        var result = await RunWindowsUtilityAsync("winepath.exe", ["-u", windowsPath], _defaultWorkingDirectory, cancellationToken);
        EnsureSuccess(result, $"Could not convert Windows path '{windowsPath}' to a host path in the compatibility prefix.");
        var converted = result.StandardOutput.Trim();
        if (converted.Length == 0)
        {
            throw new InvalidOperationException($"UMU returned an empty host path for '{windowsPath}'.");
        }

        return converted;
    }

    private async Task<string> ValidateCopyDestinationAsync(string destinationPath, CancellationToken cancellationToken)
    {
        if (IsPathWithin(_prefixPath, destinationPath))
        {
            return Path.GetFullPath(_prefixPath);
        }

        string[] specialFolderRoots;
        lock (_copyRootsLock)
        {
            specialFolderRoots = _allowedCopyRoots.ToArray();
        }

        var allowedRoot = specialFolderRoots.FirstOrDefault(root => IsPathWithin(root, destinationPath));
        if (allowedRoot is null)
        {
            throw new InvalidOperationException("Post-install folder copies may write only inside the game prefix or a configured Windows special folder.");
        }

        return Path.GetFullPath(allowedRoot);
    }

    private static void EnsureNoSymlinkInDestination(string path, string allowedRoot)
    {
        EnsureContainedWithin(allowedRoot, path);
        var current = path;
        while (!string.IsNullOrEmpty(current) && !string.Equals(current, allowedRoot, StringComparison.Ordinal))
        {
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"The post-install copy destination crosses a symbolic link: {current}");
            }

            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent ?? string.Empty;
        }
    }

    private static bool IsPathWithin(string root, string candidate)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullCandidate = Path.GetFullPath(candidate);
        return fullCandidate.Equals(fullRoot, StringComparison.Ordinal)
            || fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void EnsureContainedWithin(string root, string candidate)
    {
        if (!IsPathWithin(root, candidate))
        {
            throw new IOException("The post-install copy source contains a path that escapes its destination.");
        }
    }

    [GeneratedRegex("%[^%]+%")]
    private static partial Regex UnknownTokenRegex();

    [GeneratedRegex("%(?:USER_MYDOCS|COMMON_MYDOCS|STEAMPATH|APPDATA|LOCALAPPDATA|ROOTDRIVE|WINDIR)%", RegexOptions.IgnoreCase)]
    private static partial Regex KnownTemplateTokenRegex();

    [GeneratedRegex("%(?<name>[A-Za-z_][A-Za-z0-9_]*)%")]
    private static partial Regex WindowsEnvironmentVariableRegex();
}
