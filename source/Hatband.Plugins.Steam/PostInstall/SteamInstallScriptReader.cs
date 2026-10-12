using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Games.PostInstall;
using SteamKit2;

namespace Hatband.Plugins.Steam.PostInstall;

internal static class SteamInstallScriptReader
{
    public static SteamAppManifest? ReadManifest(string manifestPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = File.OpenRead(manifestPath);
            var root = new KeyValue();
            root.ReadAsText(stream);
            var appState = FindChild(root, "AppState") ?? root;
            var stateFlags = GetValue(appState, "StateFlags");
            var isInstalled = int.TryParse(stateFlags, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags) &&
                              (flags & 4) != 0;

            return new SteamAppManifest(
                ReadNamedValues(FindChild(appState, "InstalledDepots")),
                ReadPairs(FindChild(appState, "InstallScripts")),
                ReadPairs(FindChild(appState, "SharedDepots")),
                GetValue(appState, "installdir"),
                GetValue(FindChild(appState, "UserConfig"), "language") ?? "english",
                isInstalled);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
        catch (Exception exception) when (exception.GetType() == typeof(Exception))
        {
            // SteamKit2 can report malformed KeyValue text with a plain Exception.
            // Keep this catch at the parser boundary so unrelated failures still surface.
            return null;
        }
    }

    public static SteamInstallScriptParseResult ReadScript(
        SteamInstallScriptSource source,
        byte[] content,
        uint gameAppId,
        int firstStepIndex,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<GamePostInstallDiagnostic>();
        var steps = new List<GamePostInstallStep>();
        cancellationToken.ThrowIfCancellationRequested();

        KeyValue script;
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            script = new KeyValue();
            script.ReadAsText(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            diagnostics.Add(Diagnostic(
                "SteamInstallScriptInvalid",
                $"Steam install script '{source.ScriptPath}' could not be parsed."));
            return new SteamInstallScriptParseResult(steps, diagnostics);
        }
        catch (Exception exception) when (exception.GetType() == typeof(Exception))
        {
            // SteamKit2 can report malformed KeyValue text with a plain Exception.
            // Keep this catch at the parser boundary so unrelated failures still surface.
            diagnostics.Add(Diagnostic(
                "SteamInstallScriptInvalid",
                $"Steam install script '{source.ScriptPath}' could not be parsed."));
            return new SteamInstallScriptParseResult(steps, diagnostics);
        }

        try
        {
            var rootIsInstallScript = IsName(script, "InstallScript");
            var installScriptRoots = rootIsInstallScript
                ? [script]
                : script.Children.Where(child => IsName(child, "InstallScript")).ToArray();
            if (!rootIsInstallScript)
            {
                foreach (var child in script.Children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var childName = RequiredName(child);
                    if (IsName(child, "kvsignatures"))
                    {
                        continue;
                    }

                    if (!IsName(child, "InstallScript"))
                    {
                        diagnostics.Add(IsLanguageGroup(childName)
                            ? LanguageGroupUnsupported(source, childName)
                            : Unsupported(source, $"top-level key '{childName}'"));
                    }
                }
            }

            if (installScriptRoots.Length == 0)
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptRootMissing",
                    $"Steam install script '{source.ScriptPath}' has no InstallScript root."));
                return new SteamInstallScriptParseResult(steps, diagnostics);
            }

            var stepIndex = firstStepIndex;
            foreach (var root in installScriptRoots)
            {
                var matchingLanguageRoots = new List<KeyValue>();
                var englishLanguageRoots = new List<KeyValue>();
                var defaultLanguageRoots = new List<KeyValue>();
                var languageRoots = new List<KeyValue>();
                foreach (var section in root.Children)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sectionName = RequiredName(section);
                    if (IsInstallSection(section))
                    {
                        ReadSection(section, source, gameAppId, ref stepIndex, steps, diagnostics);
                    }
                    else if (IsLanguageGroup(sectionName))
                    {
                        languageRoots.Add(section);
                        if (sectionName.Equals(source.Language, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingLanguageRoots.Add(section);
                        }
                        else if (sectionName.Equals("english", StringComparison.OrdinalIgnoreCase))
                        {
                            englishLanguageRoots.Add(section);
                        }
                        else if (sectionName.Equals("default", StringComparison.OrdinalIgnoreCase))
                        {
                            defaultLanguageRoots.Add(section);
                        }
                    }
                    else
                    {
                        diagnostics.Add(Unsupported(source, $"InstallScript section '{sectionName}'"));
                    }
                }

                var selectedLanguageRoots = matchingLanguageRoots.Count > 0
                    ? matchingLanguageRoots
                : englishLanguageRoots.Count > 0
                    ? englishLanguageRoots
                    : defaultLanguageRoots;
                if (selectedLanguageRoots.Count == 0 && languageRoots.Count > 0)
                {
                    var availableLanguages = string.Join(", ", languageRoots.Select(languageRoot => RequiredName(languageRoot)));
                    diagnostics.Add(Diagnostic(
                        "SteamInstallScriptLanguageUnavailable",
                        $"Steam install script '{source.ScriptPath}' has language groups [{availableLanguages}], but none matches installed language '{source.Language}' or the default group."));
                }

                foreach (var languageRoot in selectedLanguageRoots)
                {
                    foreach (var section in languageRoot.Children)
                    {
                        var sectionName = RequiredName(section);
                        if (!IsInstallSection(section))
                        {
                            diagnostics.Add(Unsupported(source,
                                $"section '{sectionName}' in selected language group '{RequiredName(languageRoot)}'"));
                            continue;
                        }

                        ReadSection(section, source, gameAppId, ref stepIndex, steps, diagnostics);
                    }
                }
            }

            return new SteamInstallScriptParseResult(steps, diagnostics);
        }
        catch (FormatException exception)
        {
            diagnostics.Add(Diagnostic(
                "SteamInstallScriptNameMissing",
                $"Steam install script '{source.ScriptPath}' contains an unnamed key: {exception.Message}"));
            return new SteamInstallScriptParseResult([], diagnostics);
        }
    }

    private static void ReadSection(
        KeyValue section,
        SteamInstallScriptSource source,
        uint gameAppId,
        ref int stepIndex,
        ICollection<GamePostInstallStep> steps,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        if (IsName(section, "Registry"))
        {
            ReadRegistry(section, source, gameAppId, ref stepIndex, steps, diagnostics);
        }
        else if (IsName(section, "Run Process"))
        {
            ReadRunProcesses(section, source, gameAppId, ref stepIndex, steps, diagnostics);
        }
        else if (IsName(section, "Copy Folders"))
        {
            ReadCopyFolders(section, source, gameAppId, ref stepIndex, steps, diagnostics);
        }
    }

    private static bool IsInstallSection(KeyValue section)
    {
        return IsName(section, "Registry") || IsName(section, "Run Process") || IsName(section, "Copy Folders");
    }

    public static byte[] GetManifestRevisionBytes(SteamAppManifest manifest)
    {
        var content = new StringBuilder();
        content.Append("Installed=").Append(manifest.IsInstalled ? "1" : "0").Append('\n');
        if (manifest.InstallDirectoryName is null)
        {
            content.Append("InstallDirectoryMissing=1\n");
        }
        else
        {
            content.Append("InstallDirectory=").Append(manifest.InstallDirectoryName).Append('\n');
        }
        content.Append("Language=").Append(manifest.Language).Append('\n');
        foreach (var depot in manifest.InstalledDepots)
        {
            content.Append("InstalledDepot=").Append(depot).Append('\n');
        }

        foreach (var entry in manifest.InstallScripts)
        {
            content.Append("InstallScript=").Append(entry.Name).Append("|HasValue=")
                .Append(entry.Value is null ? "0" : "1").Append("|Value=").Append(entry.Value).Append('\n');
        }

        foreach (var entry in manifest.SharedDepots)
        {
            content.Append("SharedDepot=").Append(entry.Name).Append("|HasValue=")
                .Append(entry.Value is null ? "0" : "1").Append("|Value=").Append(entry.Value).Append('\n');
        }

        return Encoding.UTF8.GetBytes(content.ToString());
    }

    public static string CreateRevision(
        IReadOnlyList<(string Path, byte[] Content)> revisionParts,
        IReadOnlyList<GamePostInstallDiagnostic> diagnostics)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (path, content) in revisionParts)
        {
            Append(hash, Encoding.UTF8.GetBytes(Path.GetFullPath(path)));
            Append(hash, content);
        }

        Append(hash, Encoding.UTF8.GetBytes("diagnostics"));
        foreach (var diagnostic in diagnostics
                     .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                     .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal))
        {
            Append(hash, Encoding.UTF8.GetBytes(diagnostic.Code));
            Append(hash, Encoding.UTF8.GetBytes(diagnostic.Message));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void ReadRegistry(
        KeyValue registrySection,
        SteamInstallScriptSource source,
        uint gameAppId,
        ref int stepIndex,
        ICollection<GamePostInstallStep> steps,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        foreach (var registryKey in registrySection.Children)
        {
            var keyPath = RequiredName(registryKey);
            if (!IsSupportedRegistryKey(keyPath))
            {
                diagnostics.Add(Unsupported(source, $"registry key path '{keyPath}'"));
                continue;
            }

            var values = new List<GamePostInstallRegistryEntry>();
            foreach (var valueType in registryKey.Children)
            {
                if (IsName(valueType, "string"))
                {
                    ReadRegistryValues(valueType, true, source, values, diagnostics);
                }
                else if (IsName(valueType, "dword"))
                {
                    ReadRegistryValues(valueType, false, source, values, diagnostics);
                }
                else
                {
                    diagnostics.Add(Unsupported(source, $"registry value type '{RequiredName(valueType)}'"));
                }
            }

            var index = stepIndex++;
            steps.Add(new GamePostInstallRegistryStep
            {
                Id = CreateStepId(source, gameAppId, index),
                Name = $"Registry: {keyPath}",
                InstallDirectory = source.InstallDirectory,
                KeyPath = NormalizeRegistryKeyPath(keyPath),
                View = GamePostInstallRegistryView.Registry32,
                Values = values
            });
        }
    }

    private static void ReadRegistryValues(
        KeyValue valueType,
        bool isString,
        SteamInstallScriptSource source,
        ICollection<GamePostInstallRegistryEntry> values,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        var matchingLanguageValues = new List<KeyValue>();
        var englishLanguageValues = new List<KeyValue>();
        var defaultLanguageValues = new List<KeyValue>();
        var languageGroups = new List<string>();
        var hasMatchingLanguageGroup = false;
        var hasEnglishLanguageGroup = false;
        var hasDefaultLanguageGroup = false;
        foreach (var value in valueType.Children)
        {
            var valueName = RequiredName(value);
            if (IsLanguageGroup(valueName))
            {
                languageGroups.Add(valueName);
                if (valueName.Equals(source.Language, StringComparison.OrdinalIgnoreCase))
                {
                    hasMatchingLanguageGroup = true;
                    matchingLanguageValues.AddRange(value.Children);
                }
                else if (valueName.Equals("english", StringComparison.OrdinalIgnoreCase))
                {
                    hasEnglishLanguageGroup = true;
                    englishLanguageValues.AddRange(value.Children);
                }
                else if (valueName.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    hasDefaultLanguageGroup = true;
                    defaultLanguageValues.AddRange(value.Children);
                }

                continue;
            }

            if (value.Children.Count > 0)
            {
                diagnostics.Add(Unsupported(source,
                    $"nested registry value group '{valueName}' under '{RequiredName(valueType)}'"));
                continue;
            }

            AddRegistryValue(value, isString, source, values, diagnostics);
        }

        var localizedValues = hasMatchingLanguageGroup
            ? matchingLanguageValues
            : hasEnglishLanguageGroup
                ? englishLanguageValues
                : defaultLanguageValues;
        if (!hasMatchingLanguageGroup && !hasEnglishLanguageGroup && !hasDefaultLanguageGroup && languageGroups.Count > 0)
        {
            diagnostics.Add(Diagnostic(
                "SteamInstallScriptLanguageUnavailable",
                $"Steam install script '{source.ScriptPath}' has registry language groups [{string.Join(", ", languageGroups)}], but none matches installed language '{source.Language}' or the default group."));
        }

        foreach (var value in localizedValues)
        {
            AddRegistryValue(value, isString, source, values, diagnostics);
        }
    }

    private static void AddRegistryValue(
        KeyValue value,
        bool isString,
        SteamInstallScriptSource source,
        ICollection<GamePostInstallRegistryEntry> values,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        var name = RequiredName(value);
        if (isString)
        {
            if (value.Value is not { } stringValue)
            {
                diagnostics.Add(Diagnostic(
                    "SteamRegistryStringMissing",
                    $"Registry string '{name}' in '{source.ScriptPath}' has no value."));
                return;
            }

            values.Add(new GamePostInstallStringRegistryEntry
            {
                Name = NormalizeRegistryValueName(name),
                Value = stringValue
            });
            return;
        }

        if (!TryParseUnsignedInteger(value.Value, out var parsedValue))
        {
            diagnostics.Add(Diagnostic(
                "SteamRegistryDwordInvalid",
                $"Registry DWORD '{name}' in '{source.ScriptPath}' is not a valid 32-bit integer."));
            return;
        }

        values.Add(new GamePostInstallDwordRegistryEntry
        {
            Name = NormalizeRegistryValueName(name),
            Value = parsedValue
        });
    }

    private static void ReadRunProcesses(
        KeyValue runProcessSection,
        SteamInstallScriptSource source,
        uint gameAppId,
        ref int stepIndex,
        ICollection<GamePostInstallStep> steps,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        foreach (var installer in runProcessSection.Children)
        {
            var installerName = RequiredName(installer);
            var processes = new SortedDictionary<int, string>();
            var commands = new Dictionary<int, string>();
            string? hasRunKey = null;
            uint minimumHasRunValue = 1;
            var ignoreExitCode = false;
            var noCleanUp = false;
            GamePostInstallCondition? condition = null;

            foreach (var value in installer.Children)
            {
                if (TryGetIndexedValue(value, "Process", out var processIndex, out var processPath))
                {
                    if (string.IsNullOrWhiteSpace(processPath))
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamInstallScriptProcessPathMissing",
                            $"Process {processIndex} in '{installerName}' in '{source.ScriptPath}' has no executable path."));
                        continue;
                    }

                    if (!processes.TryAdd(processIndex, processPath))
                    {
                        diagnostics.Add(Unsupported(source, $"duplicate Process {processIndex} in '{installerName}'"));
                    }
                }
                else if (TryGetIndexedValue(value, "Command", out var commandIndex, out var commandText))
                {
                    if (commandText is null)
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamInstallScriptCommandValueMissing",
                            $"Command {commandIndex} in '{installerName}' in '{source.ScriptPath}' has no value."));
                        continue;
                    }

                    if (!commands.TryAdd(commandIndex, commandText))
                    {
                        diagnostics.Add(Unsupported(source, $"duplicate Command {commandIndex} in '{installerName}'"));
                    }
                }
                else if (IsName(value, "HasRunKey"))
                {
                    if (value.Value is not { Length: > 0 } registryKey)
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamHasRunKeyMissing",
                            $"HasRunKey in '{installerName}' in '{source.ScriptPath}' has no registry key."));
                    }
                    else if (!IsSupportedRegistryKey(registryKey))
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamHasRunKeyInvalid",
                            $"HasRunKey '{registryKey}' in '{source.ScriptPath}' is not a supported registry key path."));
                    }
                    else
                    {
                        hasRunKey = NormalizeRegistryKeyPath(registryKey);
                    }
                }
                else if (IsName(value, "MinimumHasRunValue"))
                {
                    if (!TryParseUnsignedInteger(value.Value, out minimumHasRunValue))
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamMinimumHasRunValueInvalid",
                            $"MinimumHasRunValue in '{source.ScriptPath}' is not a valid integer."));
                        minimumHasRunValue = 1;
                    }
                }
                else if (IsName(value, "IgnoreExitCode"))
                {
                    ignoreExitCode = ParseBoolean(value.Value, source, "IgnoreExitCode", diagnostics);
                }
                else if (IsName(value, "NoCleanUp"))
                {
                    noCleanUp = ParseBoolean(value.Value, source, "NoCleanUp", diagnostics);
                }
                else if (IsName(value, "Requirement_OS"))
                {
                    condition = ReadRequirement(value, source, diagnostics);
                }
                else
                {
                    diagnostics.Add(Unsupported(source, $"Run Process property '{RequiredName(value)}' in '{installerName}'"));
                }
            }

            foreach (var commandIndex in commands.Keys.Where(index => !processes.ContainsKey(index)))
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptCommandWithoutProcess",
                    $"Command {commandIndex} in '{installerName}' has no matching Process {commandIndex} in '{source.ScriptPath}'."));
            }

            if (processes.Count == 0)
            {
                diagnostics.Add(Diagnostic(
                    "SteamInstallScriptProcessMissing",
                    $"Run Process entry '{installerName}' in '{source.ScriptPath}' does not contain a Process N value."));
                continue;
            }

            var processList = processes
                .Select(process => new GamePostInstallProcess
                {
                    ExecutableTemplate = process.Value,
                    Arguments = commands.TryGetValue(process.Key, out var command) ? command : null
                })
                .ToArray();
            var stepIndexForId = stepIndex++;
            steps.Add(new GamePostInstallInstallerStep
            {
                Id = CreateStepId(source, gameAppId, stepIndexForId),
                Name = installerName,
                InstallDirectory = source.InstallDirectory,
                Condition = condition,
                Processes = processList,
                HasRunKey = hasRunKey,
                RegistryView = GamePostInstallRegistryView.Registry32,
                MinimumHasRunValue = minimumHasRunValue,
                IgnoreExitCode = ignoreExitCode,
                NoCleanUp = noCleanUp
            });
        }
    }

    private static void ReadCopyFolders(
        KeyValue copyFoldersSection,
        SteamInstallScriptSource source,
        uint gameAppId,
        ref int stepIndex,
        ICollection<GamePostInstallStep> steps,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        foreach (var folder in copyFoldersSection.Children)
        {
            var folderName = RequiredName(folder);
            var sources = new SortedDictionary<int, string>();
            var destinations = new SortedDictionary<int, string>();
            foreach (var value in folder.Children)
            {
                if (TryGetIndexedValue(value, "SrcFolder", out var sourceIndex, out var sourcePath))
                {
                    if (string.IsNullOrWhiteSpace(sourcePath))
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamCopyFolderSourceMissing",
                            $"SrcFolder {sourceIndex} in '{folderName}' in '{source.ScriptPath}' has no path."));
                        continue;
                    }

                    if (!sources.TryAdd(sourceIndex, sourcePath))
                    {
                        diagnostics.Add(Unsupported(source, $"duplicate SrcFolder {sourceIndex} in '{folderName}'"));
                    }
                }
                else if (TryGetIndexedValue(value, "DstFolder", out var destinationIndex, out var destinationPath))
                {
                    if (string.IsNullOrWhiteSpace(destinationPath))
                    {
                        diagnostics.Add(Diagnostic(
                            "SteamCopyFolderDestinationMissing",
                            $"DstFolder {destinationIndex} in '{folderName}' in '{source.ScriptPath}' has no path."));
                        continue;
                    }

                    if (!destinations.TryAdd(destinationIndex, destinationPath))
                    {
                        diagnostics.Add(Unsupported(source, $"duplicate DstFolder {destinationIndex} in '{folderName}'"));
                    }
                }
                else
                {
                    diagnostics.Add(Unsupported(source, $"Copy Folders property '{RequiredName(value)}' in '{folderName}'"));
                }
            }

            foreach (var sourcePair in sources)
            {
                if (!destinations.TryGetValue(sourcePair.Key, out var destinationPath))
                {
                    diagnostics.Add(Diagnostic(
                        "SteamCopyFolderDestinationMissing",
                        $"SrcFolder {sourcePair.Key} in '{folderName}' has no matching DstFolder {sourcePair.Key} in '{source.ScriptPath}'."));
                    continue;
                }

                var stepIndexForId = stepIndex++;
                steps.Add(new GamePostInstallCopyFolderStep
                {
                    Id = CreateStepId(source, gameAppId, stepIndexForId),
                    Name = folderName,
                    InstallDirectory = source.InstallDirectory,
                    SourceTemplate = sourcePair.Value,
                    DestinationTemplate = destinationPath
                });
            }

            foreach (var destinationIndex in destinations.Keys.Where(index => !sources.ContainsKey(index)))
            {
                diagnostics.Add(Diagnostic(
                    "SteamCopyFolderSourceMissing",
                    $"DstFolder {destinationIndex} in '{folderName}' has no matching SrcFolder {destinationIndex} in '{source.ScriptPath}'."));
            }
        }
    }

    private static GamePostInstallCondition? ReadRequirement(
        KeyValue requirement,
        SteamInstallScriptSource source,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        bool? is64BitWindows = null;
        foreach (var value in requirement.Children)
        {
            if (IsName(value, "Is64BitWindows"))
            {
                is64BitWindows = ParseBoolean(value.Value, source, "Requirement_OS.Is64BitWindows", diagnostics);
            }
            else
            {
                diagnostics.Add(Unsupported(source, $"Requirement_OS property '{RequiredName(value)}'"));
            }
        }

        return is64BitWindows is null ? null : new GamePostInstallCondition
        {
            Is64BitWindows = is64BitWindows,
            OperatingSystem = HostOperatingSystem.Windows
        };
    }

    private static bool ParseBoolean(
        string? value,
        SteamInstallScriptSource source,
        string propertyName,
        ICollection<GamePostInstallDiagnostic> diagnostics)
    {
        if (value == "1" || bool.TryParse(value, out var parsed) && parsed)
        {
            return true;
        }

        if (value == "0" || bool.TryParse(value, out parsed) && !parsed)
        {
            return false;
        }

        diagnostics.Add(Diagnostic(
            "SteamInstallScriptBooleanInvalid",
            $"'{propertyName}' in '{source.ScriptPath}' is not a valid Steam boolean."));
        return false;
    }

    private static bool TryParseUnsignedInteger(string? value, out uint parsedValue)
    {
        parsedValue = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(
                trimmedValue.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        return uint.TryParse(trimmedValue, NumberStyles.None, CultureInfo.InvariantCulture, out parsedValue);
    }

    private static bool TryGetIndexedValue(
        KeyValue value,
        string prefix,
        out int index,
        out string? text)
    {
        index = 0;
        text = null;
        var valueName = RequiredName(value);
        if (!valueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = valueName[prefix.Length..].Trim();
        if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out index) || index <= 0)
        {
            return false;
        }

        text = value.Value;
        return true;
    }

    private static bool IsSupportedRegistryKey(string keyPath)
    {
        var separator = keyPath.IndexOf('\\');
        if (separator < 0)
        {
            return false;
        }

        var root = keyPath[..separator];
        return root.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("HKCC", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRegistryKeyPath(string keyPath)
    {
        return keyPath.Replace('/', '\\').TrimEnd('\\');
    }

    private static string NormalizeRegistryValueName(string name)
    {
        return name.Equals("(Default)", StringComparison.OrdinalIgnoreCase) ? string.Empty : name;
    }

    private static string CreateStepId(SteamInstallScriptSource source, uint gameAppId, int stepIndex)
    {
        var context = string.Join(
            "|",
            gameAppId.ToString(CultureInfo.InvariantCulture),
            source.OwnerAppId,
            source.DepotId,
            source.ScriptOrdinal.ToString(CultureInfo.InvariantCulture),
            Path.GetFullPath(source.OwnerManifestPath),
            Path.GetFullPath(source.ScriptPath),
            stepIndex.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(context));
        return $"steam:{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static void Append(IncrementalHash hash, byte[] value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    private static bool IsName(KeyValue value, string name)
    {
        return string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    private static KeyValue? FindChild(KeyValue? parent, string name)
    {
        return parent?.Children.FirstOrDefault(child => IsName(child, name));
    }

    private static string? GetValue(KeyValue? parent, string name)
    {
        var value = FindChild(parent, name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> ReadNamedValues(KeyValue? parent)
    {
        return parent?.Children.Select(RequiredName).ToArray() ?? [];
    }

    private static IReadOnlyList<SteamManifestEntry> ReadPairs(KeyValue? parent)
    {
        if (parent is null)
        {
            return [];
        }

        var pairs = new List<SteamManifestEntry>();
        foreach (var child in parent.Children)
        {
            pairs.Add(new SteamManifestEntry(RequiredName(child), child.Value));
        }

        return pairs;
    }

    private static GamePostInstallDiagnostic Unsupported(SteamInstallScriptSource source, string construct)
    {
        return Diagnostic(
            "SteamInstallScriptUnsupportedConstruct",
            $"Steam install script '{source.ScriptPath}' contains unsupported {construct}. It was not converted into an action.");
    }

    private static GamePostInstallDiagnostic LanguageGroupUnsupported(
        SteamInstallScriptSource source,
        string groupName)
    {
        return Diagnostic(
            "SteamInstallScriptLanguageGroupUnsupported",
            $"Steam install script '{source.ScriptPath}' contains language group '{groupName}'. The installed language is '{source.Language}', but this group was not converted into actions.");
    }

    private static bool IsLanguageGroup(string? name)
    {
        return name is not null && (name.Equals("default", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("english", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("german", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("french", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("italian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("spanish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("russian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("japanese", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("koreana", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("schinese", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("tchinese", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("brazilian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("polish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("portuguese", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("dutch", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("swedish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("danish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("norwegian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("finnish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("czech", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("hungarian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("romanian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("thai", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("turkish", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("ukrainian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("vietnamese", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("greek", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("bulgarian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("indonesian", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("latam", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("arabic", StringComparison.OrdinalIgnoreCase));
    }

    private static GamePostInstallDiagnostic Diagnostic(string code, string message)
    {
        return new GamePostInstallDiagnostic { Code = code, Message = message };
    }

    private static string RequiredName(KeyValue value)
    {
        return value.Name ?? throw new FormatException("A key-value entry has no name.");
    }
}

internal sealed record SteamAppManifest(
    IReadOnlyList<string> InstalledDepots,
    IReadOnlyList<SteamManifestEntry> InstallScripts,
    IReadOnlyList<SteamManifestEntry> SharedDepots,
    string? InstallDirectoryName,
    string Language,
    bool IsInstalled);

internal sealed record SteamManifestEntry(string Name, string? Value);

internal sealed record SteamInstallScriptParseResult(
    IReadOnlyList<GamePostInstallStep> Steps,
    IReadOnlyList<GamePostInstallDiagnostic> Diagnostics);
