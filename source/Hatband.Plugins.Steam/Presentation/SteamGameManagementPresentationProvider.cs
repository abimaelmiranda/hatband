using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Settings;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Plugins.Steam.Settings;

namespace Hatband.Plugins.Steam.Presentation;

public sealed class SteamGameManagementPresentationProvider : IGameManagementPresentationProvider
{
    private readonly ISettingsApi settings;

    public SteamGameManagementPresentationProvider(ISettingsApi settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    public string SourceId => GameSourceIds.Steam;

    public async Task<bool> KeepHostInForegroundAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var configuration = await settings.GetSectionAsync<SteamSettings>(cancellationToken);
        return configuration.SilentModeEnabled;
    }
}
