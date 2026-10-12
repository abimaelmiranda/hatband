using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Services.Games;

public sealed class NativeGameLaunchRuntime : IGameLaunchRuntime
{
    private readonly IHostApplicationLauncher _launcher;
    private readonly IHostSystemInfo _hostSystemInfo;

    public NativeGameLaunchRuntime(IHostApplicationLauncher launcher, IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        _launcher = launcher;
        _hostSystemInfo = hostSystemInfo;
    }

    public string RuntimeId => "native";

    public HostOperatingSystem HostOperatingSystem => _hostSystemInfo.Platform;

    public HostOperatingSystem TargetOperatingSystem => _hostSystemInfo.Platform;

    public bool RequiresCompatibilityTool => false;

    public bool SupportsPostInstall => false;

    public GameRuntimeProcessWatchMode ProcessWatchMode => GameRuntimeProcessWatchMode.Direct;

    public Task<HostApplicationProcess?> LaunchAsync(
        GameRuntimeLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Tool is not null)
        {
            throw new InvalidOperationException("The native runtime cannot launch with a compatibility tool.");
        }

        GameLaunchEnvironmentPolicy.ValidateUserVariables(request.UserEnvironmentVariables);
        foreach (var (name, value) in request.Command.EnvironmentVariables)
        {
            if (!GameLaunchEnvironmentPolicy.IsValidVariableName(name) || value.Contains('\0'))
            {
                throw new InvalidOperationException($"The launch provider supplied an invalid environment variable '{name}'.");
            }
        }

        if (!Path.IsPathFullyQualified(request.Command.ExecutablePath) || !File.Exists(request.Command.ExecutablePath))
        {
            throw new FileNotFoundException("The configured game executable must be an existing absolute path.", request.Command.ExecutablePath);
        }

        var arguments = request.Command.Arguments;
        if (!string.IsNullOrWhiteSpace(request.AdditionalArguments))
        {
            arguments = string.IsNullOrWhiteSpace(arguments)
                ? request.AdditionalArguments
                : $"{arguments} {request.AdditionalArguments}";
        }

        var environmentVariables = request.Command.EnvironmentVariables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        foreach (var (name, value) in request.UserEnvironmentVariables)
        {
            environmentVariables[name] = value;
        }

        var workingDirectory = request.Command.WorkingDirectory ?? Path.GetDirectoryName(request.Command.ExecutablePath)
            ?? throw new InvalidOperationException("The selected executable has no parent directory.");
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"The configured game working directory does not exist: {workingDirectory}");
        }

        return _launcher.StartApplicationAsync(
            request.Command.ExecutablePath,
            arguments,
            workingDirectory,
            environmentVariables,
            cancellationToken);
    }
}
