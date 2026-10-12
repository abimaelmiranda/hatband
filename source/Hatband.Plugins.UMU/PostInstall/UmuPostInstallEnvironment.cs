using System.Globalization;
using System.Text.RegularExpressions;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Plugins.UMU.PostInstall;

/// <summary>Runs post-install operations in the same Proton prefix UMU uses for the game.</summary>
internal sealed partial class UmuPostInstallEnvironment : IGamePostInstallEnvironment
{
    private const int MaximumOutputCharacters = 64 * 1024;

    private readonly IHostApplicationLauncher _launcher;
    private readonly string _umuExecutable;
    private readonly string _prefixPath;
    private readonly string _defaultWorkingDirectory;
    private readonly IReadOnlyDictionary<string, string> _environmentVariables;
    public UmuPostInstallEnvironment(
        IHostApplicationLauncher launcher,
        string umuExecutable,
        string prefixPath,
        string defaultWorkingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(umuExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultWorkingDirectory);
        ArgumentNullException.ThrowIfNull(environmentVariables);
        _launcher = launcher;
        _umuExecutable = umuExecutable;
        _prefixPath = prefixPath;
        _defaultWorkingDirectory = defaultWorkingDirectory;
        var runnerEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in environmentVariables)
        {
            runnerEnvironment[name] = value;
        }

        runnerEnvironment["PROTON_VERB"] = "runinprefix";
        _environmentVariables = runnerEnvironment;
    }

    public async Task<bool> Is64BitWindowsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunWindowsUtilityAsync(
            "cmd.exe",
            ["/d", "/c", "echo %PROCESSOR_ARCHITECTURE%"],
            _defaultWorkingDirectory,
            cancellationToken);
        EnsureSuccess(result, "Could not determine the Windows architecture of the compatibility prefix.");
        var architecture = result.StandardOutput.Trim();
        if (architecture.Equals("AMD64", StringComparison.OrdinalIgnoreCase)
            || architecture.Equals("ARM64", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (architecture.Equals("x86", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"UMU returned an unsupported Windows architecture value '{Limit(architecture)}'.");
    }

    public async Task<string> ResolvePathAsync(
        string template,
        string installDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ValidateAbsolutePath(installDirectory, nameof(installDirectory));

        var expanded = await ExpandTemplateAsync(template, installDirectory, cancellationToken);
        if (Path.IsPathFullyQualified(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        expanded = expanded.Replace('/', '\\');
        var nativePath = await ConvertWindowsPathToNativeAsync(expanded, cancellationToken);
        if (!Path.IsPathFullyQualified(nativePath))
        {
            throw new InvalidOperationException($"UMU returned a non-absolute host path for '{Limit(template)}'.");
        }

        return Path.GetFullPath(nativePath);
    }

    public async Task<string> ResolveStringAsync(
        string template,
        string installDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ValidateAbsolutePath(installDirectory, nameof(installDirectory));
        return await ExpandTemplateAsync(template, installDirectory, cancellationToken);
    }

    public async Task<GamePostInstallRegistryEntry?> ReadRegistryValueAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        string valueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(valueName);
        var result = await RunWindowsUtilityAsync(
            "reg.exe",
            valueName.Length == 0
                ? ["query", keyPath, "/ve", GetRegistryViewArgument(view)]
                : ["query", keyPath, "/v", valueName, GetRegistryViewArgument(view)],
            _defaultWorkingDirectory,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            var output = $"{result.StandardOutput}\n{result.StandardError}";
            if (MissingRegistryValueRegex().IsMatch(output))
            {
                return null;
            }

            ThrowCommandFailure("Could not read the requested prefix registry value", result);
        }

        return ParseRegistryValue(result.StandardOutput, valueName);
    }

    public async Task EnsureRegistryKeyAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        var result = await RunWindowsUtilityAsync(
            "reg.exe",
            ["add", keyPath, "/f", GetRegistryViewArgument(view)],
            _defaultWorkingDirectory,
            cancellationToken);
        EnsureSuccess(result, $"Could not create prefix registry key '{keyPath}'.");
    }

    public async Task SetRegistryValueAsync(
        string keyPath,
        GamePostInstallRegistryView view,
        GamePostInstallRegistryEntry value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(value);

        var arguments = new List<string> { "add", keyPath };
        if (value.Name.Length == 0)
        {
            arguments.Add("/ve");
        }
        else
        {
            arguments.Add("/v");
            arguments.Add(value.Name);
        }

        switch (value)
        {
            case GamePostInstallStringRegistryEntry stringValue:
                arguments.Add("/t");
                arguments.Add("REG_SZ");
                arguments.Add("/d");
                arguments.Add(stringValue.Value);
                break;
            case GamePostInstallDwordRegistryEntry dwordValue:
                arguments.Add("/t");
                arguments.Add("REG_DWORD");
                arguments.Add("/d");
                arguments.Add(dwordValue.Value.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unsupported registry value type '{value.GetType().Name}'.");
        }

        arguments.Add("/f");
        arguments.Add(GetRegistryViewArgument(view));
        var result = await RunWindowsUtilityAsync("reg.exe", arguments, _defaultWorkingDirectory, cancellationToken);
        EnsureSuccess(result, $"Could not write prefix registry value '{value.Name}' in '{keyPath}'.");
    }

    public async Task<GamePostInstallProcessResult> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ValidateAbsolutePath(executablePath, nameof(executablePath));
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The post-install executable does not exist.", executablePath);
        }

        var nativeWorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? _defaultWorkingDirectory;
        ValidateAbsolutePath(nativeWorkingDirectory, nameof(workingDirectory));
        var windowsExecutable = await ConvertNativePathToWindowsAsync(executablePath, cancellationToken);

        IReadOnlyList<string> processArguments;
        var extension = Path.GetExtension(executablePath);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            ValidateBatchArguments(windowsExecutable, arguments);
            processArguments = ["cmd.exe", "/d", "/c", "call", windowsExecutable, .. arguments];
        }
        else
        {
            processArguments = [windowsExecutable, .. arguments];
        }

        var result = await _launcher.RunApplicationAsync(
            _umuExecutable,
            processArguments,
            nativeWorkingDirectory,
            _environmentVariables,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new GamePostInstallProcessResult(
            result.ExitCode,
            Limit(result.StandardOutput),
            Limit(result.StandardError));
    }

    public async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ValidateAbsolutePath(sourceDirectory, nameof(sourceDirectory));
        ValidateAbsolutePath(destinationDirectory, nameof(destinationDirectory));
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"The post-install copy source does not exist: {sourceDirectory}");
        }

        var canonicalSource = Path.GetFullPath(sourceDirectory);
        var canonicalDestination = Path.GetFullPath(destinationDirectory);
        if (IsPathWithin(canonicalSource, canonicalDestination) || IsPathWithin(canonicalDestination, canonicalSource))
        {
            throw new IOException("The post-install copy source and destination cannot overlap.");
        }

        if ((File.GetAttributes(canonicalSource) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The post-install copy source is a symbolic link and cannot be copied safely: {canonicalSource}");
        }

        var destinationRoot = await ValidateCopyDestinationAsync(canonicalDestination, cancellationToken);
        EnsureNoSymlinkInDestination(canonicalDestination, destinationRoot);
        Directory.CreateDirectory(canonicalDestination);

        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(canonicalSource, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(sourcePath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"The post-install copy source contains a symbolic link that cannot be copied safely: {sourcePath}");
            }

            var relativePath = Path.GetRelativePath(canonicalSource, sourcePath);
            var targetPath = Path.GetFullPath(Path.Combine(canonicalDestination, relativePath));
            EnsureContainedWithin(canonicalDestination, targetPath);
            EnsureNoSymlinkInDestination(targetPath, destinationRoot);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException("A copied file must have a parent directory.");
            Directory.CreateDirectory(parentDirectory);
            if (!File.Exists(targetPath))
            {
                File.Copy(sourcePath, targetPath, overwrite: false);
            }
        }
    }

    private async Task<HostApplicationResult> RunWindowsUtilityAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // Wine utility output is parsed below; keep its language stable without
        // changing the locale of dependency installers or the game itself.
        var utilityEnvironment = new Dictionary<string, string>(_environmentVariables, StringComparer.Ordinal)
        {
            ["LC_ALL"] = "C",
            ["LANG"] = "C",
            ["LANGUAGE"] = "en"
        };
        return await _launcher.RunApplicationAsync(
            _umuExecutable,
            [executable, .. arguments],
            workingDirectory,
            utilityEnvironment,
            cancellationToken);
    }

    private static GamePostInstallRegistryEntry ParseRegistryValue(string output, string valueName)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = RegistryValueRegex().Match(line);
            if (!match.Success || !string.Equals(match.Groups["name"].Value, valueName.Length == 0 ? "(Default)" : valueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = valueName;
            var type = match.Groups["type"].Value;
            var data = match.Groups["data"].Value;
            if (type.Equals("REG_DWORD", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = data.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt32(data[2..], 16)
                    : uint.Parse(data, CultureInfo.InvariantCulture);
                return new GamePostInstallDwordRegistryEntry { Name = name, Value = parsed };
            }

            if (type is "REG_SZ" or "REG_EXPAND_SZ")
            {
                return new GamePostInstallStringRegistryEntry { Name = name, Value = data };
            }

            throw new InvalidOperationException($"The prefix registry value '{valueName}' has unsupported type '{type}'.");
        }

        throw new InvalidOperationException($"UMU reported a successful registry query but did not return value '{valueName}'.");
    }

    private static string GetRegistryViewArgument(GamePostInstallRegistryView view)
    {
        return view switch
        {
            GamePostInstallRegistryView.Registry32 => "/reg:32",
            GamePostInstallRegistryView.Registry64 => "/reg:64",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The registry view is not supported.")
        };
    }

    private static void ValidateBatchArguments(string executable, IReadOnlyList<string> arguments)
    {
        foreach (var value in arguments.Prepend(executable))
        {
            if (value.IndexOfAny(['"', '\r', '\n', '&', '|', '<', '>', '^', '%', '!']) >= 0)
            {
                throw new InvalidOperationException("The batch installer path or arguments contain unsupported cmd.exe control characters.");
            }
        }
    }

    private static void ThrowCommandFailure(string message, HostApplicationResult result)
    {
        var details = string.Join(Environment.NewLine, new[] { result.StandardError.Trim(), result.StandardOutput.Trim() }.Where(value => value.Length > 0));
        var suffix = details.Length == 0 ? string.Empty : $" {Limit(details)}";
        throw new InvalidOperationException($"{message}.{suffix}");
    }

    private static void EnsureSuccess(HostApplicationResult result, string message)
    {
        if (result.ExitCode != 0)
        {
            ThrowCommandFailure(message, result);
        }
    }

    private static void ValidateAbsolutePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The path must be absolute.", parameterName);
        }
    }

    private static string Limit(string value)
    {
        if (value.Length <= MaximumOutputCharacters)
        {
            return value;
        }

        return value[^MaximumOutputCharacters..];
    }

    [GeneratedRegex("^\\s*(?<name>.*?)\\s+(?<type>REG_[A-Z0-9_]+)\\s+(?<data>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex RegistryValueRegex();

    [GeneratedRegex("(?:ERROR: The system was unable to find the specified registry key or value|reg: Unable to find the specified registry (?:key|value))", RegexOptions.IgnoreCase)]
    private static partial Regex MissingRegistryValueRegex();

}
