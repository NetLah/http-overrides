namespace NetLah.Extensions.HttpOverrides;

public static class Config
{
    public const string HttpOverridesKey = "HttpOverrides";
    public const string HttpLoggingKey = "HttpLogging";

    public const string AspNetCoreForwardedHeadersEnabledKey = "ASPNETCORE_FORWARDEDHEADERS_ENABLED";
    
    public const string ClearForwardLimitKey = "ClearForwardLimit";

    public const string KnownNetworksKey = "KnownNetworks";
    public const string ClearKnownNetworksKey = "ClearKnownNetworks";

    public const string KnownProxiesKey = "KnownProxies";
    public const string ClearKnownProxiesKey = "ClearKnownProxies";

    public const string HttpLoggingEnabledKey = "HttpLoggingEnabled";
    public const string ClearRequestHeadersKey = "ClearRequestHeaders";
    public const string ClearResponseHeadersKey = "ClearResponseHeaders";
}
