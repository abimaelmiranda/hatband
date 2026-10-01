using System.Globalization;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Maps BCP-47 language tags to the language identifiers used by Steam's store APIs.
/// </summary>
internal static class SteamLanguage
{
    public static (string StoreLanguage, string ContentLanguageTag) Resolve(string languageTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);

        var culture = CultureInfo.GetCultureInfo(languageTag);
        var language = culture.TwoLetterISOLanguageName;
        var region = culture.Name.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .FirstOrDefault();

        return language switch
        {
            "ar" => ("arabic", "ar"),
            "bg" => ("bulgarian", "bg"),
            "cs" => ("czech", "cs"),
            "da" => ("danish", "da"),
            "de" => ("german", "de"),
            "el" => ("greek", "el"),
            "en" => ("english", "en"),
            "es" => ("spanish", "es"),
            "fi" => ("finnish", "fi"),
            "fr" => ("french", "fr"),
            "hu" => ("hungarian", "hu"),
            "id" => ("indonesian", "id"),
            "it" => ("italian", "it"),
            "ja" => ("japanese", "ja"),
            "ko" => ("koreana", "ko"),
            "nl" => ("dutch", "nl"),
            "no" => ("norwegian", "no"),
            "pl" => ("polish", "pl"),
            "pt" => ResolvePortuguese(region),
            "ro" => ("romanian", "ro"),
            "ru" => ("russian", "ru"),
            "sv" => ("swedish", "sv"),
            "th" => ("thai", "th"),
            "tr" => ("turkish", "tr"),
            "uk" => ("ukrainian", "uk"),
            "vi" => ("vietnamese", "vi"),
            "zh" => ResolveChinese(region),
            _ => ("english", "en")
        };
    }

    private static (string StoreLanguage, string ContentLanguageTag) ResolvePortuguese(string? region)
    {
        return string.Equals(region, "BR", StringComparison.OrdinalIgnoreCase)
            ? ("brazilian", "pt-BR")
            : ("portuguese", "pt-PT");
    }

    private static (string StoreLanguage, string ContentLanguageTag) ResolveChinese(string? region)
    {
        var isTraditionalChinese = string.Equals(region, "TW", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(region, "HK", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(region, "MO", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(region, "Hant", StringComparison.OrdinalIgnoreCase);
        if (isTraditionalChinese)
        {
            return ("tchinese", "zh-TW");
        }

        return ("schinese", "zh-CN");
    }
}
