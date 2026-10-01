using Hatband.Core.Models.Settings;

namespace Hatband.Core.Abstractions;

public interface ISettingsStore
{
    Task<HatbandSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(HatbandSettings settings, CancellationToken cancellationToken = default);
}
