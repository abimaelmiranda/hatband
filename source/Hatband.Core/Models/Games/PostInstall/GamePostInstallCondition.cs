using Hatband.Core.Enums.Host;

namespace Hatband.Core.Models.Games.PostInstall;

public sealed record GamePostInstallCondition
{
    public bool? Is64BitWindows { get; init; }

    public HostOperatingSystem? OperatingSystem { get; init; }
}
