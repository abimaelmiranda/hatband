using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions.Compatibility;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Integrations.HowLongToBeat;
using Hatband.Core.Abstractions.Settings;
using Hatband.SDK.Plugins;

namespace Hatband.Infrastructure.Plugins;

internal static class PluginCapabilityTypes
{
    public static IReadOnlyList<Type> Get(PluginLoadingOptions options)
    {
        return new Type[]
        {
            typeof(ICompatibilityToolReleaseProvider),
            typeof(ICompatibilityToolLocationProvider),
            typeof(ICompatibilityToolDiscoveryProvider),
            typeof(ICompatibilityToolInstallationProvider),
            typeof(IGameLaunchRuntime),
            typeof(IGameStoreIntegration),
            typeof(IGameSourceInfo),
            typeof(IGameMetadataProvider),
            typeof(IGameArtworkProvider),
            typeof(IGameInstallationProvider),
            typeof(IGameManagementProvider),
            typeof(IGamePostInstallProvider),
            typeof(IGameManagementPresentationProvider),
            typeof(IConnectorAuthenticationCapability),
            typeof(IConnectorSessionProvider),
            typeof(IHowLongToBeatProvider),
            typeof(ISettingsSection),
        }.Concat(options.AdditionalCapabilityTypes).Distinct().ToArray();
    }
}
