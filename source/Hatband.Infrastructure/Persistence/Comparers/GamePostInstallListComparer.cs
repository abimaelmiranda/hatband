using System.Text.Json;
using Hatband.Core.Models.Games.PostInstall;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hatband.Infrastructure.Persistence.Comparers;

internal static class GamePostInstallListComparer
{
    public static ValueComparer<IReadOnlyList<GamePostInstallStep>> Steps { get; } = new(
        (left, right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right),
        value => JsonSerializer.Serialize(value).GetHashCode(),
        value => DeserializeSteps(JsonSerializer.Serialize(value)));

    public static ValueComparer<IReadOnlyList<GamePostInstallDiagnostic>> Diagnostics { get; } = new(
        (left, right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right),
        value => JsonSerializer.Serialize(value).GetHashCode(),
        value => DeserializeDiagnostics(JsonSerializer.Serialize(value)));

    public static IReadOnlyList<GamePostInstallStep> DeserializeSteps(string value)
    {
        return JsonSerializer.Deserialize<List<GamePostInstallStep>>(value)
            ?? throw new InvalidOperationException("The saved post-install steps are invalid.");
    }

    public static IReadOnlyList<GamePostInstallDiagnostic> DeserializeDiagnostics(string value)
    {
        return JsonSerializer.Deserialize<List<GamePostInstallDiagnostic>>(value)
            ?? throw new InvalidOperationException("The saved post-install diagnostics are invalid.");
    }
}
