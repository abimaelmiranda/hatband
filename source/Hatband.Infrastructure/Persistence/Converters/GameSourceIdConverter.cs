using Hatband.Core.Enums.Stores;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hatband.Infrastructure.Persistence.Converters;

public sealed class GameSourceIdConverter : ValueConverter<GameSourceId, string>
{
    public GameSourceIdConverter()
        : base(
            sourceId => sourceId.ToString().ToLowerInvariant(),
            sourceId => Enum.Parse<GameSourceId>(sourceId, ignoreCase: true))
    {
    }
}
