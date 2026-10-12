using Hatband.Core.Models.Games;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Core.Abstractions.Games;

public interface IGamePostInstallService
{
    void ValidateDefinition(GamePostInstallPlan plan);

    Task PrepareAsync(
        GamePostInstallPlan plan,
        IGamePostInstallEnvironment environment,
        string prefixPath,
        IProgress<GamePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
