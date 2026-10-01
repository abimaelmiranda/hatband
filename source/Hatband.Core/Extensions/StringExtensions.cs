using System.Globalization;
using System.Text;

namespace Hatband.Core.Extensions;

public static class StringExtensions
{
    public static string NormalizeForLooseComparison(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposedValue = value.Normalize(NormalizationForm.FormD);
        var normalizedValue = new StringBuilder(decomposedValue.Length);

        foreach (var character in decomposedValue)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalizedValue.Append(char.ToLowerInvariant(character));
            }
            else if (normalizedValue.Length > 0 && normalizedValue[^1] != ' ')
            {
                normalizedValue.Append(' ');
            }
        }

        return normalizedValue.ToString().Trim();
    }

    public static string? PreferNonWhiteSpace(this string? preferred, string? fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
    }
}
