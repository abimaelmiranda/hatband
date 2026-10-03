using System.Runtime.InteropServices;

namespace Hatband.Integrations.Proton;

internal static class ProtonArchitecture
{
    public static string GetAssetArchitectureName(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => throw new PlatformNotSupportedException(
                $"Proton releases are not available for operating system architecture '{architecture}'.")
        };
    }
}
