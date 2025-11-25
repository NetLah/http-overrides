namespace NetLah.Extensions.HttpOverrides;

public static class DefaultConfiguration
{
    public const string HealthChecksConst = "/healthz";
    public const string HealthChecksAzureAppServiceContainer = "/robots933456.txt";

    public const string HealthCheckKey = "HealthCheck";
    public const string HttpOverridesKey = "HttpOverrides";
    public const string HttpLoggingKey = "HttpLogging";

    /// <summary>
    /// Setttings ASPNETCORE_FORWARDEDHEADERS_ENABLED
    /// </summary>
    public const string AspNetCoreForwardedHeadersEnabledKey = "ForwardedHeaders_Enabled";

    public const string HttpOverridesLogLevelKey = "LogLevel";

    public const string ClearForwardLimitKey = "ClearForwardLimit";

    public const string KnownNetworksKey = "KnownNetworks";
    public const string ClearKnownNetworksKey = "ClearKnownNetworks";
#if NET10_0_OR_GREATER
    public const string KnownIPNetworksKey = "KnownIPNetworks";
    public const string ClearKnownIPNetworksKey = "ClearKnownIPNetworks";
#endif

    public const string KnownProxiesKey = "KnownProxies";
    public const string ClearKnownProxiesKey = "ClearKnownProxies";

    public const string HttpLoggingEnabledKey = "HttpLoggingEnabled";
    public const string HttpLoggingLogLevelKey = "LogLevel";
    public const string ClearRequestHeadersKey = "ClearRequestHeaders";
    public const string ClearResponseHeadersKey = "ClearResponseHeaders";

    public static string HealthChecksPath { get; } = HealthChecksConst;
}
