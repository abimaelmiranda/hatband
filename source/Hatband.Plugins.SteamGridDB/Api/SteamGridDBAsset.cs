using Hatband.Core.Enums.Artwork;

namespace Hatband.Plugins.SteamGridDB.Api;

public sealed record SteamGridDBAsset(Uri Url, GameArtworkSlot Slot, string ContentType);
