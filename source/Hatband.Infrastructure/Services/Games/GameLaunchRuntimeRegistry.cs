using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Host;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameLaunchRuntimeRegistry : IGameLaunchRuntimeRegistry
{
    private readonly IReadOnlyDictionary<(string RuntimeId, HostOperatingSystem Host, HostOperatingSystem Target), IGameLaunchRuntime> _runtimes;

    public GameLaunchRuntimeRegistry(IEnumerable<IGameLaunchRuntime> runtimes)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        _runtimes = runtimes.ToDictionary(
            runtime => (runtime.RuntimeId, runtime.HostOperatingSystem, runtime.TargetOperatingSystem));
    }

    public IGameLaunchRuntime Resolve(
        HostOperatingSystem hostOperatingSystem,
        HostOperatingSystem targetOperatingSystem,
        string? runtimeId = null)
    {
        if (runtimeId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeId);
            if (_runtimes.TryGetValue((runtimeId, hostOperatingSystem, targetOperatingSystem), out var runtime))
            {
                return runtime;
            }

            throw new InvalidOperationException(
                $"No game launch runtime '{runtimeId}' is registered for {hostOperatingSystem} to {targetOperatingSystem}.");
        }

        var matchingRuntimes = _runtimes.Values
            .Where(runtime => runtime.HostOperatingSystem == hostOperatingSystem &&
                runtime.TargetOperatingSystem == targetOperatingSystem)
            .ToArray();
        return matchingRuntimes.Length switch
        {
            1 => matchingRuntimes[0],
            0 => throw new InvalidOperationException(
                $"No game launch runtime is registered for {hostOperatingSystem} to {targetOperatingSystem}."),
            _ => throw new InvalidOperationException(
                $"Multiple game launch runtimes support {hostOperatingSystem} to {targetOperatingSystem}; select a runtime explicitly.")
        };
    }

    public bool Supports(
        HostOperatingSystem hostOperatingSystem,
        HostOperatingSystem targetOperatingSystem)
    {
        return _runtimes.Keys.Any(key => key.Host == hostOperatingSystem && key.Target == targetOperatingSystem);
    }
}
