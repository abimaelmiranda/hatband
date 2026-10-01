namespace Hatband.Integrations.HowLongToBeat;

internal static class HowLongToBeatProtocol
{
    public static readonly Uri SiteBaseUri = new("https://howlongtobeat.com/");

    public const string SearchPathFallback = "/api/search/site";
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36";
}
