using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
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
    private static HealthCheckAppOptions _healthCheckAppOptions = default!;
    private static bool _isForwardedHeadersEnabled;
    private static bool _isHttpLoggingEnabled;

    public static WebApplicationBuilder AddHttpOverrides(this WebApplicationBuilder webApplicationBuilder,
        string httpOverridesSectionName = DefaultConfiguration.HttpOverridesKey,
        string httpLoggingSectionName = DefaultConfiguration.HttpLoggingKey,
        string healthCheckSectionName = DefaultConfiguration.HealthCheckKey)
    {
        webApplicationBuilder.Services.AddHttpOverrides(webApplicationBuilder.Configuration, httpOverridesSectionName, httpLoggingSectionName, healthCheckSectionName);
        return webApplicationBuilder;
    }

    public static IServiceCollection AddHttpOverrides(this IServiceCollection services, IConfiguration configuration,
        string httpOverridesSectionName = DefaultConfiguration.HttpOverridesKey,
        string httpLoggingSectionName = DefaultConfiguration.HttpLoggingKey,
        string healthCheckSectionName = DefaultConfiguration.HealthCheckKey)
    {
        ILogger? logger = null;

        void EnsureLogger()
        {
            logger ??= _loggerLazy.Value;
            logger ??= NullLogger.Instance;
        }

        var healthCheckConfigurationSection = string.IsNullOrEmpty(healthCheckSectionName) ? configuration : configuration.GetSection(healthCheckSectionName);
        var healthCheckAppOptions = _healthCheckAppOptions = healthCheckConfigurationSection.Get<HealthCheckAppOptions>() ?? new HealthCheckAppOptions();
        if (healthCheckAppOptions.IsEnabled)
        {
            EnsureLogger();
            if (healthCheckConfigurationSection.GetChildren().Any())
            {
                logger?.LogDebug("Attempt to load HealthCheckOptions from configuration");
                services.Configure<HealthCheckOptions>(healthCheckConfigurationSection);
            }
            else
            {
                logger?.LogDebug("Add HealthChecks");
            }

            services.Configure<HealthCheckAppOptions>(healthCheckConfigurationSection);

            services.AddHealthChecks();     // Registers health checks services
        }

        _isForwardedHeadersEnabled = configuration[DefaultConfiguration.AspNetCoreForwardedHeadersEnabledKey].IsTrue();

        if (!_isForwardedHeadersEnabled)
        {
            EnsureLogger();
            logger?.LogDebug("Attempt to load ForwardedHeadersOptions from configuration");

            var httpOverridesConfigurationSection = string.IsNullOrEmpty(httpOverridesSectionName) ? configuration : configuration.GetSection(httpOverridesSectionName);
            services.Configure<ForwardedHeadersOptions>(httpOverridesConfigurationSection);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                if (httpOverridesConfigurationSection[DefaultConfiguration.ClearForwardLimitKey].IsTrue())
                {
                    options.ForwardLimit = null;
                }

                ProcessKnownProxies(httpOverridesConfigurationSection, options);

                ProcessKnownNetworks(httpOverridesConfigurationSection, options);
            });
        }

        var httpLoggingEnabledKey = string.IsNullOrEmpty(httpLoggingSectionName) ? DefaultConfiguration.HttpLoggingEnabledKey : $"{httpLoggingSectionName}:Enabled";
        _isHttpLoggingEnabled = configuration[httpLoggingEnabledKey].IsTrue();

        if (_isHttpLoggingEnabled)
        {
            EnsureLogger();
            logger?.LogDebug("Attempt to load HttpLoggingOptions from configuration");

            var httpLoggingConfigurationSection = string.IsNullOrEmpty(httpLoggingSectionName) ? configuration : configuration.GetSection(httpLoggingSectionName);
            services.Configure<HttpLoggingOptions>(httpLoggingConfigurationSection);
            var isClearRequestHeaders = httpLoggingConfigurationSection[DefaultConfiguration.ClearRequestHeadersKey].IsTrue();
            var isClearResponseHeaders = httpLoggingConfigurationSection[DefaultConfiguration.ClearResponseHeadersKey].IsTrue();
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

    public static WebApplication UseHttpOverrides(this WebApplication app, ILogger? logger = null)
    {
        logger ??= _loggerLazy.Value;
        logger ??= NullLogger.Instance;
        var sp = app.Services;
        var optionsForwardedHeadersOptions = sp.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var fho = optionsForwardedHeadersOptions.Value;

        if (_healthCheckAppOptions.IsEnabled)
        {
            if (_healthCheckAppOptions.IsDefaultAzure)
            {
                _healthCheckAppOptions.Paths = new[] { DefaultConfiguration.HealthChecksPath, DefaultConfiguration.HealthChecksAzureContainer };
            }

            var port = _healthCheckAppOptions.Port;
            if (_healthCheckAppOptions.Paths is { } pathArrays && pathArrays.Length > 0)
            {
                var paths = pathArrays.Select(p => (PathString)p).ToArray();
                logger.LogDebug("Use HealthChecks Port:{port} {paths}", port, paths);

                bool predicate(HttpContext c)
                {
                    if (port == null || c.Connection.LocalPort == port)
                    {
                        foreach (var path in paths)
                        {
                            if (c.Request.Path.StartsWithSegments(path, out var remaining) &&
                                string.IsNullOrEmpty(remaining))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                app.MapWhen(predicate, b => b.UseMiddleware<HealthCheckMiddleware>(Array.Empty<object>()));
            }
            else
            {
                var healthChecksPath = StringHelper.NormalizeNull(_healthCheckAppOptions.Path);
                logger.LogDebug("Use HealthChecks Port:{port} {path}", port, healthChecksPath);
                if (port.HasValue)
                {
                    app.UseHealthChecks(healthChecksPath, port.Value);
                }
                else
                {
                    app.UseHealthChecks(healthChecksPath);
                }
            }
        }

        var hostFilteringOptions = sp.GetRequiredService<IOptions<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>>();
        if (hostFilteringOptions?.Value is { } hostFiltering)
        {
            logger.LogInformation("HostFiltering: {@hostFiltering}", hostFiltering);
        }

        if (_isForwardedHeadersEnabled)
        {
            var bypassNetLahHttpOverridesMessage = $"Bypass HttpOverrides configuration settings because {DefaultConfiguration.AspNetCoreForwardedHeadersEnabledKey} is True";
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
        var knownNetworks = configuration[DefaultConfiguration.KnownNetworksKey];
        if (knownNetworks != null || configuration[DefaultConfiguration.ClearKnownNetworksKey].IsTrue())
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
        var knownProxies = configuration[DefaultConfiguration.KnownProxiesKey];
        if (knownProxies != null || configuration[DefaultConfiguration.ClearKnownProxiesKey].IsTrue())
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