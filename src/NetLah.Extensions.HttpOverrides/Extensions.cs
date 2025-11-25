using System.Net;
#if NET10_0_OR_GREATER
using IPNetwork = System.Net.IPNetwork;
#else
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
#endif

namespace NetLah.Extensions.HttpOverrides;

internal static class Extensions
{
    private static readonly char[] Separator = new char[] { ',', '|', ';', ' ' };

    public static bool IsTrue(this string? configurationValue)
    {
        return string.Equals(configurationValue, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFalse(this string? configurationValue)
    {
        return string.Equals(configurationValue, "false", StringComparison.OrdinalIgnoreCase);
    }

    public static HashSet<string> SplitSet(this string? configurationValue)
    {
        return configurationValue
            ?.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Where(i => !string.IsNullOrEmpty(i))
            .ToHashSet()
            ?? [];
    }

#if NET10_0_OR_GREATER
    public static string? ToStringComma(this IList<IPNetwork>? value)
    {
        return value == null ? null : string.Join(",", value.Select(ToString));

        static string ToString(IPNetwork ipNetwork)
        {
            return $"{ipNetwork.BaseAddress}/{ipNetwork.PrefixLength}";
        }
    }
#else
    public static string? ToStringComma(this IList<IPNetwork>? value)
    {
        return value == null ? null : string.Join(",", value.Select(ToString));

        static string ToString(IPNetwork ipNetwork)
        {
            return $"{ipNetwork.Prefix}/{ipNetwork.PrefixLength}";
        }
    }
#endif

    public static string? ToStringComma(this IList<IPAddress>? value)
    {
        return value == null ? null : string.Join(",", value);
    }
}
