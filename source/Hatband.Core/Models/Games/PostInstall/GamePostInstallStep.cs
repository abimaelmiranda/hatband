using System.Text.Json.Serialization;

namespace Hatband.Core.Models.Games.PostInstall;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$step")]
[JsonDerivedType(typeof(GamePostInstallRegistryStep), "registry")]
[JsonDerivedType(typeof(GamePostInstallInstallerStep), "installer")]
[JsonDerivedType(typeof(GamePostInstallCopyFolderStep), "copyFolder")]
public abstract record GamePostInstallStep
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Absolute install directory used to resolve source placeholders. Refresh this when the game moves.
    /// </summary>
    public required string InstallDirectory { get; init; }

    public GamePostInstallCondition? Condition { get; init; }
}
