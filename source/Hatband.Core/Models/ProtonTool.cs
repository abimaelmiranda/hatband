using Hatband.Core.Enums;

namespace Hatband.Core.Models;

public sealed record ProtonTool(
    string Name,
    string Version,
    string InstallationPath,
    ProtonToolSource Source);
