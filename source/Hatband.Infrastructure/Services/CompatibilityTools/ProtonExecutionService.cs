using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Text;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Services.CompatibilityTools;

/// <summary>
/// Launches manually-added games through the configured Proton tool and UMU.
/// </summary>
public sealed class ProtonExecutionService
{
    private readonly IHostApplicationLauncher _hostApplicationLauncher;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly UmuDependencyService _umuDependencyService;
    private readonly ILogger<ProtonExecutionService> _logger;
    private readonly ConcurrentDictionary<Guid, Task<int>> _processCompletionByGameId = new();

    public ProtonExecutionService(
        IHostApplicationLauncher hostApplicationLauncher,
        IHostSystemInfo hostSystemInfo,
        UmuDependencyService umuDependencyService,
        ILogger<ProtonExecutionService> logger)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(umuDependencyService);
        ArgumentNullException.ThrowIfNull(logger);
        _hostApplicationLauncher = hostApplicationLauncher;
        _hostSystemInfo = hostSystemInfo;
        _umuDependencyService = umuDependencyService;
        _logger = logger;
    }

    public async Task<bool> LaunchAsync(
        Game game,
        string executable,
        string? arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (!_hostSystemInfo.IsLinux)
        {
            throw new PlatformNotSupportedException("Proton is only available for manual games on Linux.");
        }

        if (game.SourceId != GameSourceId.Manual)
        {
            throw new InvalidOperationException("Proton execution is only available for manually added games.");
        }

        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
        {
            throw new FileNotFoundException("The configured game executable must be an existing absolute path.", executable);
        }

        var parsedArguments = ParseArguments(arguments);

        var compatibilityTool = game.CompatibilityTool
            ?? throw new InvalidOperationException($"No Proton compatibility tool is configured for '{game.Name}'.");
        var prefix = game.CompatibilityPrefix
            ?? throw new InvalidOperationException($"No Proton prefix is configured for '{game.Name}'.");

        ValidateProtonTool(compatibilityTool);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(prefix.Path);
        var umuExecutable = await _umuDependencyService.ResolveExecutableAsync(cancellationToken);
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PROTONPATH"] = compatibilityTool.InstallationPath,
            ["WINEPREFIX"] = prefix.Path,
            ["GAMEID"] = "umu-default",
            ["PROTON_VERB"] = "waitforexitandrun",
            ["UMU_LOG"] = "1",
            ["HATBAND_GAME_ID"] = game.Id.ToString("N"),
            ["STEAM_COMPAT_LIBRARY_PATHS"] = string.Join(
                Path.PathSeparator,
                new[] { Path.GetDirectoryName(executable), compatibilityTool.InstallationPath, prefix.Path }
                    .Where(path => !string.IsNullOrWhiteSpace(path)))
        };

        var processArguments = new List<string> { executable };
        processArguments.AddRange(parsedArguments);
        var launch = await _hostApplicationLauncher.StartApplicationAsync(
            umuExecutable,
            processArguments,
            workingDirectory ?? Path.GetDirectoryName(executable),
            environmentVariables,
            cancellationToken);
        if (launch is null)
        {
            return false;
        }

        _processCompletionByGameId[game.Id] = launch.Completion;
        _ = LogProcessCompletionAsync(game.Id, game.Name, launch.Completion);
        return true;
    }

    public int? GetCompletedExitCode(Guid gameId)
    {
        if (!_processCompletionByGameId.TryGetValue(gameId, out var completion) || !completion.IsCompleted)
        {
            return null;
        }

        return completion.GetAwaiter().GetResult();
    }

    public void ForgetLaunch(Guid gameId)
    {
        _processCompletionByGameId.TryRemove(gameId, out _);
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

    [SupportedOSPlatform("linux")]
    private static void ValidateProtonTool(CompatibilityTool compatibilityTool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compatibilityTool.InstallationPath);
        var protonScript = Path.Combine(compatibilityTool.InstallationPath, "proton");
        if (!Directory.Exists(compatibilityTool.InstallationPath) || !File.Exists(protonScript))
        {
            throw new DirectoryNotFoundException($"The selected Proton tool '{compatibilityTool.Name}' is no longer installed.");
        }

        var mode = File.GetUnixFileMode(protonScript);
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            throw new InvalidOperationException(
                $"The selected Proton tool '{compatibilityTool.Name}' is not executable. Reinstall it from Settings > Proton, then try again.");
        }
    }

    private async Task LogProcessCompletionAsync(Guid gameId, string gameName, Task<int> completion)
    {
        int exitCode;
        try
        {
            exitCode = await completion;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not observe the Proton runner process for {GameName} ({GameId}).", gameName, gameId);
            return;
        }

        if (exitCode != 0)
        {
            _logger.LogWarning("Proton runner for {GameName} ({GameId}) exited with code {ExitCode}.", gameName, gameId, exitCode);
        }
        else
        {
            _logger.LogInformation("Proton runner for {GameName} ({GameId}) exited successfully.", gameName, gameId);
        }
    }
}
