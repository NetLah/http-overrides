namespace NetLah.Extensions.HttpOverrides;

internal static class StringHelper
{
    public static string GetOrDefault(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    public static string? NormalizeNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? default : value.Trim();
}
