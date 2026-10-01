namespace Hatband.Integrations.IGN;

internal sealed record IgnGraphQlResult<T>(T? Value, string? ErrorMessage)
    where T : class
{
    public static IgnGraphQlResult<T> Succeeded(T value) => new(value, null);

    public static IgnGraphQlResult<T> Failed(string errorMessage) => new(null, errorMessage);
}
