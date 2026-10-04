using Hatband.Core.Models;

namespace Hatband.Core.Abstractions.Integrations.HowLongToBeat;

/// <summary>
/// Searches HowLongToBeat for completion time estimates by game title.
/// </summary>
public interface IHowLongToBeatProvider
{
    Task<IReadOnlyList<HowLongToBeatGame>> SearchAsync(
        string gameName,
        CancellationToken cancellationToken = default);
}
