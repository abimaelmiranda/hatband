using Hatband.Core.Enums.Games;

namespace Hatband.Core.Models.Compatibility;

public sealed record CompatibilityToolLocation(
    string DirectoryPath,
    CompatibilityToolSource Source,
    bool LinkToManagedDirectory,
    bool CanInstall);
