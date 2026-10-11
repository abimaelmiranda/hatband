using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Integrations.HowLongToBeat;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Infrastructure.Plugins;

internal static class PluginCapabilityTypes
{
    public static readonly Type[] All =
    [
        typeof(ICompatibilityToolReleaseProvider),
        typeof(IGameCompatibilityRuntime),
        typeof(IGameStoreIntegration),
        typeof(IGameMetadataProvider),
        typeof(IGameArtworkProvider),
        typeof(IGameInstallationProvider),
        typeof(IGameManagementProvider),
        typeof(IGamePostInstallProvider),
        typeof(IConnectorAuthenticationCapability),
        typeof(IQrCodeLoginProvider),
        typeof(IConnectorSessionProvider),
        typeof(IHowLongToBeatProvider),
        typeof(ISettingsSection),
    ];
}
