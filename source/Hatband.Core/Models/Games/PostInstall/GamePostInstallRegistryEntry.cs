using System.Text.Json.Serialization;

namespace Hatband.Core.Models.Games.PostInstall;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$valueType")]
[JsonDerivedType(typeof(GamePostInstallStringRegistryEntry), "string")]
[JsonDerivedType(typeof(GamePostInstallDwordRegistryEntry), "dword")]
public abstract record GamePostInstallRegistryEntry
{
    /// <summary>
    /// Empty name addresses the registry key's default value.
    /// </summary>
    public required string Name { get; init; }
}
