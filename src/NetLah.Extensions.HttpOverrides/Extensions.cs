using System.Net;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace NetLah.Extensions.HttpOverrides;

internal static class Extensions
{
    public static bool IsTrue(this string? configurationValue) => string.Equals(configurationValue, "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsFalse(this string? configurationValue) => string.Equals(configurationValue, "false", StringComparison.OrdinalIgnoreCase);

    public static HashSet<string> SplitSet(this string? configurationValue)
        => configurationValue
                ?.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(i => !string.IsNullOrEmpty(i))
                .ToHashSet() ?? new HashSet<string>();

    public static string? ToStringComma(this IList<IPNetwork>? value)
    {
        return value == null ? null : string.Join(",", value.Select(ToString));

        static string ToString(IPNetwork ipNetwork) => $"{ipNetwork.Prefix}/{ipNetwork.PrefixLength}";
    }

    public static string? ToStringComma(this IList<IPAddress>? value) => value == null ? null : string.Join(",", value);
}
