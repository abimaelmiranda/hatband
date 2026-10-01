namespace Hatband.Integrations.HowLongToBeat;

internal static class HowLongToBeatResponseStatus
{
    public static void EnsureSuccess(
        HttpResponseMessage response,
        Uri requestUri,
        string? apiPath = null)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var exception = new HttpRequestException(
            $"HowLongToBeat request to '{requestUri}' returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
            null,
            response.StatusCode);
        if (apiPath is not null)
        {
            exception.Data["HltbApiPath"] = apiPath;
        }

        throw exception;
    }
}
