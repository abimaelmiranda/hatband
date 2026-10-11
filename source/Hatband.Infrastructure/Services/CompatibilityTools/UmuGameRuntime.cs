using System.Text;
using System.Runtime.Versioning;
using System.Collections.Concurrent;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Models.Games;
using Microsoft.Extensions.Logging;
using Hatband.Infrastructure.Services.Games.PostInstall;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

public sealed class UmuGameRuntime : IGameCompatibilityRuntime
{
    private readonly IHostApplicationLauncher _launcher;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IAppDataFileSystem _appDataFileSystem;
    private readonly UmuDependencyService _umuDependencyService;
    private readonly ILogger<UmuGameRuntime> _logger;
    private readonly GamePostInstallService _postInstallService;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _prefixLocks = new();

    public UmuGameRuntime(
        IHostApplicationLauncher launcher,
        IHostSystemInfo hostSystemInfo,
        IAppDataFileSystem appDataFileSystem,
        UmuDependencyService umuDependencyService,
        GamePostInstallService postInstallService,
        ILogger<UmuGameRuntime> logger)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        ArgumentNullException.ThrowIfNull(umuDependencyService);
        ArgumentNullException.ThrowIfNull(postInstallService);
        ArgumentNullException.ThrowIfNull(logger);
        _launcher = launcher;
        _hostSystemInfo = hostSystemInfo;
        _appDataFileSystem = appDataFileSystem;
        _umuDependencyService = umuDependencyService;
        _postInstallService = postInstallService;
        _logger = logger;
    }

    public async Task<HostApplicationProcess?> LaunchAsync(
        GameRuntimeLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_hostSystemInfo.IsLinux)
        {
            throw new PlatformNotSupportedException("The UMU compatibility runtime is only available on Linux.");
        }

        ValidateRequest(request);
        if (request.PostInstallPlan is not null)
        {
            _postInstallService.ValidateDefinition(request.PostInstallPlan);
        }

        var processArguments = new List<string> { request.Command.ExecutablePath };
        processArguments.AddRange(ParseArguments(request.Command.Arguments));
        processArguments.AddRange(ParseArguments(request.AdditionalArguments));
        var workingDirectory = request.Command.WorkingDirectory
            ?? Path.GetDirectoryName(request.Command.ExecutablePath)
            ?? throw new InvalidOperationException("The selected executable has no parent directory.");
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"The configured game working directory does not exist: {workingDirectory}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var prefixLock = _prefixLocks.GetOrAdd(request.GameId, static _ => new SemaphoreSlim(1, 1));
        await prefixLock.WaitAsync(cancellationToken);
        try
        {
            var prefixPath = _appDataFileSystem.ResolveGamePrefixPath(request.GameId, request.InstallDirectory);
            var umuExecutable = await _umuDependencyService.ResolveExecutableAsync(cancellationToken);
            var environmentVariables = CreateEnvironmentVariables(request, prefixPath);
            if (request.PostInstallPlan is null)
            {
                return await LaunchGameAsync(request, umuExecutable, processArguments, workingDirectory, environmentVariables, cancellationToken);
            }

            request.Progress?.Report(new GamePreparationProgress(
                GamePreparationStage.InitializingPrefix,
                null,
                0,
                request.PostInstallPlan.Steps.Count));
            var prefixInitialization = await _launcher.RunApplicationAsync(
                umuExecutable,
                ["wineboot.exe", "-u"],
                workingDirectory,
                environmentVariables,
                cancellationToken);
            if (prefixInitialization.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(prefixInitialization.StandardError)
                    ? prefixInitialization.StandardOutput.Trim()
                    : prefixInitialization.StandardError.Trim();
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail.Trim()}";
                throw new InvalidOperationException($"Could not initialize the UMU compatibility prefix.{suffix}");
            }

            var postInstallEnvironment = new UmuPostInstallEnvironment(
                _launcher,
                umuExecutable,
                prefixPath,
                workingDirectory,
                environmentVariables);
            await _postInstallService.PrepareAsync(
                request.PostInstallPlan,
                postInstallEnvironment,
                prefixPath,
                request.Progress,
                cancellationToken);

            return await LaunchGameAsync(request, umuExecutable, processArguments, workingDirectory, environmentVariables, cancellationToken);
        }
        finally
        {
            prefixLock.Release();
        }
    }

    private async Task<HostApplicationProcess?> LaunchGameAsync(
        GameRuntimeLaunchRequest request,
        string umuExecutable,
        IReadOnlyList<string> processArguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Launching game {GameId} with runner {RunnerName} via UMU.", request.GameId, request.Tool.Name);
        var process = await _launcher.StartApplicationAsync(
            umuExecutable,
            processArguments,
            workingDirectory,
            environmentVariables,
            cancellationToken);
        if (process is not null)
        {
            _ = LogProcessCompletionAsync(request.GameId, request.Tool.Name, process.Completion);
        }

        return process;
    }

    [SupportedOSPlatform("linux")]
    private static void ValidateRequest(GameRuntimeLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Tool);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstallDirectory);
        if (!Path.IsPathFullyQualified(request.InstallDirectory))
        {
            throw new ArgumentException("The game install directory must be absolute.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Command.ExecutablePath);
        if (!Path.IsPathFullyQualified(request.Command.ExecutablePath) || !File.Exists(request.Command.ExecutablePath))
        {
            throw new FileNotFoundException("The configured game executable must be an existing absolute path.", request.Command.ExecutablePath);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Tool.InstallationPath);
        if (!Path.IsPathFullyQualified(request.Tool.InstallationPath))
        {
            throw new ArgumentException("The selected Proton installation path must be absolute.", nameof(request));
        }

        var protonScript = Path.Combine(request.Tool.InstallationPath, "proton");
        if (!Directory.Exists(request.Tool.InstallationPath) || !File.Exists(protonScript))
        {
            throw new DirectoryNotFoundException($"The selected Proton tool '{request.Tool.Name}' is no longer installed.");
        }

        var mode = File.GetUnixFileMode(protonScript);
        if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
        {
            throw new InvalidOperationException($"The selected Proton tool '{request.Tool.Name}' is not executable.");
        }

        GameLaunchEnvironmentPolicy.ValidateUserVariables(request.UserEnvironmentVariables);
        foreach (var name in request.Command.EnvironmentVariables.Keys)
        {
            if (!GameLaunchEnvironmentPolicy.IsValidVariableName(name))
            {
                throw new InvalidOperationException($"The launch provider supplied an invalid environment variable name '{name}'.");
            }
        }
    }

    private static Dictionary<string, string> CreateEnvironmentVariables(
        GameRuntimeLaunchRequest request,
        string prefixPath)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in request.Command.EnvironmentVariables)
        {
            environmentVariables[name] = value;
        }

        foreach (var (name, value) in request.UserEnvironmentVariables)
        {
            environmentVariables[name] = value;
        }

        environmentVariables["PROTONPATH"] = request.Tool.InstallationPath;
        environmentVariables["WINEPREFIX"] = prefixPath;
        environmentVariables["PROTON_VERB"] = "waitforexitandrun";
        environmentVariables["UMU_LOG"] = "0";
        environmentVariables["UMU_USE_STEAM"] = "1";
        environmentVariables["HATBAND_GAME_ID"] = request.GameId.ToString("N");
        environmentVariables["STEAM_COMPAT_INSTALL_PATH"] = request.InstallDirectory;
        if (!environmentVariables.ContainsKey("GAMEID"))
        {
            environmentVariables["GAMEID"] = "umu-default";
        }

        if (!environmentVariables.ContainsKey("STORE"))
        {
            environmentVariables["STORE"] = string.Empty;
        }

        return environmentVariables;
    }

    private static IReadOnlyList<string> ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        var parsedArguments = new List<string>();
        var currentArgument = new StringBuilder();
        char? quote = null;
        var argumentStarted = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var character = arguments[index];
            if (character == '\\' && quote != '\'')
            {
                if (index + 1 < arguments.Length && (arguments[index + 1] == '"' || arguments[index + 1] == '\\'))
                {
                    currentArgument.Append(arguments[++index]);
                }
                else
                {
                    currentArgument.Append(character);
                }

                argumentStarted = true;
                continue;
            }

            if (quote is null && (character == '"' || character == '\''))
            {
                quote = character;
                argumentStarted = true;
                continue;
            }

            if (quote == character)
            {
                quote = null;
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                if (argumentStarted)
                {
                    parsedArguments.Add(currentArgument.ToString());
                    currentArgument.Clear();
                    argumentStarted = false;
                }

                continue;
            }

            currentArgument.Append(character);
            argumentStarted = true;
        }

        if (quote is not null)
        {
            throw new ArgumentException("The game arguments contain an unmatched quote.", nameof(arguments));
        }

        if (argumentStarted)
        {
            parsedArguments.Add(currentArgument.ToString());
        }

        return parsedArguments;
    }

    private async Task LogProcessCompletionAsync(Guid gameId, string runnerName, Task<int> completion)
    {
        try
        {
            var exitCode = await completion;
            if (exitCode == 0)
            {
                _logger.LogInformation("Game process {GameId} using runner {RunnerName} exited successfully.", gameId, runnerName);
            }
            else
            {
                _logger.LogWarning("Game process {GameId} using runner {RunnerName} exited with code {ExitCode}.", gameId, runnerName, exitCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not observe game process {GameId} using runner {RunnerName}.", gameId, runnerName);
        }
    }
}
