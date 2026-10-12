using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>Declares host-window behavior while a store performs an operation.</summary>
public interface IGameManagementPresentationProvider
{
    string SourceId { get; }

    Task<bool> KeepHostInForegroundAsync(Game game, CancellationToken cancellationToken = default);
}
