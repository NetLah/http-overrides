using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetLah.Extensions.Logging;

namespace NetLah.Extensions.HttpOverrides;

public static class HttpOverridesExtensions
{
    private static readonly Lazy<ILogger?> _loggerLazy = new(() => AppLogReference.GetAppLogLogger(typeof(HttpOverridesExtensions).Namespace));
    private static bool _isForwardedHeadersEnabled;
    private static bool _isHttpLoggingEnabled;

    public static WebApplicationBuilder AddHttpOverrides(this WebApplicationBuilder webApplicationBuilder, string httpOverridesSectionName = Config.HttpOverridesKey, string httpLoggingSectionName = Config.HttpLoggingKey)
    {
        webApplicationBuilder.Services.AddHttpOverrides(webApplicationBuilder.Configuration, httpOverridesSectionName, httpLoggingSectionName);
        return webApplicationBuilder;
    }

    public static IServiceCollection AddHttpOverrides(this IServiceCollection services, IConfiguration configuration,
        string httpOverridesSectionName = Config.HttpOverridesKey, string httpLoggingSectionName = Config.HttpLoggingKey)
    {
        _isForwardedHeadersEnabled = configuration[Config.AspNetCoreForwardedHeadersEnabledKey].IsTrue();

        if (!_isForwardedHeadersEnabled)
        {
            var httpOverridesConfigurationSection = string.IsNullOrEmpty(httpOverridesSectionName) ? configuration : configuration.GetSection(httpOverridesSectionName);
            services.Configure<ForwardedHeadersOptions>(httpOverridesConfigurationSection);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                if (httpOverridesConfigurationSection[Config.ClearForwardLimitKey].IsTrue())
                {
                    options.ForwardLimit = null;
                }

                ProcessKnownProxies(httpOverridesConfigurationSection, options);

                ProcessKnownNetworks(httpOverridesConfigurationSection, options);
            });
        }

        var httpLoggingEnabledKey = string.IsNullOrEmpty(httpLoggingSectionName) ? Config.HttpLoggingEnabledKey : $"{httpLoggingSectionName}:Enabled";
        _isHttpLoggingEnabled = configuration[httpLoggingEnabledKey].IsTrue();

        if (_isHttpLoggingEnabled)
        {
            var httpLoggingConfigurationSection = string.IsNullOrEmpty(httpLoggingSectionName) ? configuration : configuration.GetSection(httpLoggingSectionName);
            services.Configure<HttpLoggingOptions>(httpLoggingConfigurationSection);
            var isClearRequestHeaders = httpLoggingConfigurationSection[Config.ClearRequestHeadersKey].IsTrue();
            var isClearResponseHeaders = httpLoggingConfigurationSection[Config.ClearResponseHeadersKey].IsTrue();
            var httpLoggingConfig = httpLoggingConfigurationSection.Get<HttpLoggingConfig>();
            var requestHeaders = httpLoggingConfig?.RequestHeaders.SplitSet() ?? new HashSet<string>();
            var responseHeaders = httpLoggingConfig?.ResponseHeaders.SplitSet() ?? new HashSet<string>();
            var mediaTypeOptions = httpLoggingConfig?.MediaTypeOptions ?? Enumerable.Empty<string>();

            if (isClearRequestHeaders || isClearResponseHeaders || requestHeaders.Any() || responseHeaders.Any() || mediaTypeOptions.Any())
            {
                services.AddHttpLogging(options =>
                {
                    if (isClearRequestHeaders) { options.RequestHeaders.Clear(); }
                    if (isClearResponseHeaders) { options.ResponseHeaders.Clear(); }
                    options.RequestHeaders.UnionWith(requestHeaders);
                    options.ResponseHeaders.UnionWith(responseHeaders);
                    foreach (var mediaType in mediaTypeOptions)
                    {
                        if (!string.IsNullOrEmpty(mediaType))
                        {
                            options.MediaTypeOptions.AddText(mediaType);
                        }
                    }
                });
            }
        }

        return services;
    }

    public static IApplicationBuilder UseHttpOverrides(this IApplicationBuilder app, ILogger? logger = null)
    {
        logger ??= _loggerLazy.Value;
        logger ??= NullLogger.Instance;
        var sp = app.ApplicationServices;
        var optionsForwardedHeadersOptions = sp.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var fho = optionsForwardedHeadersOptions.Value;

        var hostFilteringOptions = sp.GetRequiredService<IOptions<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>>();
        if (hostFilteringOptions?.Value is { } hostFiltering)
        {
            logger.LogInformation("HostFiltering: {@hostFiltering}", hostFiltering);
        }

        if (_isForwardedHeadersEnabled)
        {
            var bypassNetLahHttpOverridesMessage = $"Bypass HttpOverrides configuration settings because {Config.AspNetCoreForwardedHeadersEnabledKey} is True";
#pragma warning disable CA2254 // Template should be a static expression
            logger.LogInformation(bypassNetLahHttpOverridesMessage);
#pragma warning restore CA2254 // Template should be a static expression
        }

        if (fho.KnownProxies.Count > 0 || fho.KnownNetworks.Count > 0 || fho.ForwardedHeaders != ForwardedHeaders.None)
        {
            logger.LogInformation("ForwardLimit: {forwardLimit}", fho.ForwardLimit);
        }

        if (fho.KnownProxies.Count > 0)
        {
            logger.LogInformation("KnownProxies: {knownProxies}", fho.KnownProxies.ToStringComma());
        }

        if (fho.KnownNetworks.Count > 0)
        {
            logger.LogInformation("KnownNetworks: {knownNetworks}", fho.KnownNetworks.ToStringComma());
        }

        if (fho.ForwardedHeaders != ForwardedHeaders.None)
        {
            var forwardedHeaders = string.Join(",", fho.ForwardedHeaders);
            if (_isForwardedHeadersEnabled)
            {
                logger.LogInformation("ForwardedHeaders: {forwardedHeaders}", forwardedHeaders);
            }
            else
            {
                app.UseForwardedHeaders();
                logger.LogInformation("Use ForwardedHeaders: {forwardedHeaders}", forwardedHeaders);
            }
        }

        if (fho.AllowedHosts.Count > 0)
        {
            var allowedHosts = string.Join(",", fho.AllowedHosts);
            logger.LogInformation("AllowedHosts: {allowedHosts}", allowedHosts);
        }

        if (_isHttpLoggingEnabled)
        {
            var httpLogging = sp.GetRequiredService<IOptions<HttpLoggingOptions>>()?.Value;
            logger.LogInformation("Use HttpLogging LoggingFields:{loggingFields} MediaTypeOptions:{mediaTypeOptions} RequestBodyLogLimit:{requestBodyLogLimit} ResponseBodyLogLimit:{responseBodyLogLimit} RequestHeaders:{requestHeaders} ResponseHeaders:{responseHeaders}",
                httpLogging?.LoggingFields, httpLogging?.MediaTypeOptions, httpLogging?.RequestBodyLogLimit, httpLogging?.ResponseBodyLogLimit,
                string.Join(',', httpLogging?.RequestHeaders ?? Enumerable.Empty<string>()),
                string.Join(',', httpLogging?.ResponseHeaders ?? Enumerable.Empty<string>()));
            app.UseHttpLogging();
        }

        return app;
    }

    private static void ProcessKnownNetworks(IConfiguration configuration, ForwardedHeadersOptions options)
    {
        var knownNetworks = configuration[Config.KnownNetworksKey];
        if (knownNetworks != null || configuration[Config.ClearKnownNetworksKey].IsTrue())
        {
            options.KnownNetworks.Clear();
        }

        foreach (var item in knownNetworks.SplitSet())
        {
            var net = item.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (net.Length == 2)
            {
                var prefix = System.Net.IPAddress.Parse(net[0]);
                var prefixLength = int.Parse(net[1]);
                options.KnownNetworks.Add(new IPNetwork(prefix, prefixLength));
            }
            else if (net.Length == 1)
            {
                var prefix = System.Net.IPAddress.Parse(net[0]);
                var prefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
                options.KnownNetworks.Add(new IPNetwork(prefix, prefixLength));
            }
        }
    }

    private static void ProcessKnownProxies(IConfiguration configuration, ForwardedHeadersOptions options)
    {
        var knownProxies = configuration[Config.KnownProxiesKey];
        if (knownProxies != null || configuration[Config.ClearKnownProxiesKey].IsTrue())
        {
            options.KnownProxies.Clear();
        }

        foreach (var item in knownProxies.SplitSet())
        {
            options.KnownProxies.Add(System.Net.IPAddress.Parse(item));
        }
    }

#pragma warning disable S3260 // Non-derived "private" classes and records should be "sealed"
#pragma warning disable S3459 // Unassigned members should be removed
#pragma warning disable S1144 // Unused private types or members should be removed
    private class HttpLoggingConfig
#pragma warning restore S3260 // Non-derived "private" classes and records should be "sealed"
    {
        public string? RequestHeaders { get; set; }
        public string? ResponseHeaders { get; set; }
        public List<string>? MediaTypeOptions { get; set; }
    }
#pragma warning restore S1144 // Unused private types or members should be removed
#pragma warning restore S3459 // Unassigned members should be removed
}