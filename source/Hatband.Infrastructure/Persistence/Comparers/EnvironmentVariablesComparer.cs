using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hatband.Infrastructure.Persistence.Comparers;

public sealed class EnvironmentVariablesComparer : ValueComparer<Dictionary<string, string>>
{
    public EnvironmentVariablesComparer()
        : base(
            (left, right) => AreEqual(left, right),
            value => ComputeHash(value),
            value => new Dictionary<string, string>(value, StringComparer.Ordinal))
    {
    }

    private static bool AreEqual(Dictionary<string, string>? left, Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeHash(Dictionary<string, string> value)
    {
        var hash = 0;
        foreach (var pair in value)
        {
            hash ^= HashCode.Combine(pair.Key, pair.Value);
        }

        return hash;
    }
}
