using Hatband.SDK.Plugins;
using Hatband.Infrastructure.Services.Games;

namespace Hatband.Infrastructure.Plugins;

public sealed class ManualGamesPlugin : HatbandPlugin
{
    public override string Id => "manual";

    public override string Name => "Manual games";

    public override IReadOnlyCollection<Type> CapabilityImplementations =>
    [
        typeof(ManualGameManagementProvider),
    ];

}
