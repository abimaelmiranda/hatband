using Hatband.SDK.Plugins;
using Hatband.Infrastructure.Services.CompatibilityTools;

namespace Hatband.Infrastructure.Plugins;

public sealed class UmuPlugin : HatbandPlugin
{
    public override string Id => "umu";

    public override string Name => "UMU";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(UmuGameRuntime),
    ];

}
