using Hatband.Core.Enums;

namespace Hatband.Core.Services;

public static class HostPlatformCompatibilityResolver
{
    public static HostPlatformCompatibilityStatus Resolve(
        GamePlatform? nativePlatforms,
        HostOperatingSystem hostOperatingSystem)
    {
        if (nativePlatforms is not GamePlatform platforms || hostOperatingSystem == HostOperatingSystem.Unknown)
        {
            return HostPlatformCompatibilityStatus.Unknown;
        }

        return hostOperatingSystem switch
        {
            HostOperatingSystem.Windows => platforms.HasFlag(GamePlatform.Windows)
                ? HostPlatformCompatibilityStatus.Native
                : HostPlatformCompatibilityStatus.Unsupported,
            HostOperatingSystem.MacOS => platforms.HasFlag(GamePlatform.MacOS)
                ? HostPlatformCompatibilityStatus.Native
                : HostPlatformCompatibilityStatus.Unsupported,
            HostOperatingSystem.Linux => ResolveLinuxCompatibility(platforms),
            _ => HostPlatformCompatibilityStatus.Unknown
        };
    }

    private static HostPlatformCompatibilityStatus ResolveLinuxCompatibility(GamePlatform platforms)
    {
        if (platforms.HasFlag(GamePlatform.Linux))
        {
            return HostPlatformCompatibilityStatus.Native;
        }

        return platforms.HasFlag(GamePlatform.Windows)
            ? HostPlatformCompatibilityStatus.RequiresProton
            : HostPlatformCompatibilityStatus.Unsupported;
    }
}
