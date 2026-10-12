namespace Hatband.Core.Abstractions.Games;

public interface IGameSourceCatalog
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    string GetDisplayName(string sourceId);

    int GetStoreId(string sourceId);

    string? GetLogoPath(string sourceId);
}
