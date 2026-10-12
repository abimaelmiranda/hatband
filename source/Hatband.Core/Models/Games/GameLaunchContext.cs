using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;

namespace Hatband.Core.Models.Games;

public sealed record GameLaunchContext(
    HostOperatingSystem HostOperatingSystem,
    GameExecutionMode ExecutionMode);
