using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Core.Abstractions.Games;

public interface IGamePostInstallProvider
{
    GameSourceId SourceId { get; }

    Task<GamePostInstallPlan?> GetPlanAsync(Game game, CancellationToken cancellationToken = default);
}
