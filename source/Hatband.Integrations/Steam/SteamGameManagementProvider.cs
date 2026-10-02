using System.Globalization;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Integrations.Steam;

public sealed class SteamGameManagementProvider : IGameManagementProvider
{
    private readonly IHostApplicationLauncher hostApplicationLauncher;

    public SteamGameManagementProvider(IHostApplicationLauncher hostApplicationLauncher)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        this.hostApplicationLauncher = hostApplicationLauncher;
    }

    public GameSourceId SourceId => GameSourceId.Steam;

    public Task<GameManagementResult> InstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return OpenSteamActionAsync(game, "install", cancellationToken);
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return OpenSteamActionAsync(game, "uninstall", cancellationToken);
    }

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        return OpenSteamActionAsync(game, "run", cancellationToken);
    }

    private async Task<GameManagementResult> OpenSteamActionAsync(
        Game game,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        if (game.SourceId != GameSourceId.Steam ||
            !uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            return GameManagementResult.Unavailable;
        }

        var steamUri = new Uri($"steam://{action}/{appId.ToString(CultureInfo.InvariantCulture)}");
        if (await hostApplicationLauncher.TryOpenUriAsync(steamUri, cancellationToken))
        {
            return GameManagementResult.ProtocolOpened;
        }

        if (await hostApplicationLauncher.TryLaunchStoreClientAsync(GameSourceId.Steam, cancellationToken))
        {
            return GameManagementResult.StoreClientOpened;
        }

        return GameManagementResult.Unavailable;
    }
}
