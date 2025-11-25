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
#if NET10_0_OR_GREATER
using IPNetwork = System.Net.IPNetwork;
#else
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
#endif

namespace NetLah.Extensions.HttpOverrides;

public static class HttpOverridesExtensions
{
    private static readonly Lazy<ILogger?> _loggerLazy = new(() => AppLogReference.GetAppLogLogger(typeof(HttpOverridesExtensions).Namespace));
    private static HealthCheckAppOptions? _healthCheckAppOptions;
    private static bool _isForwardedHeadersEnabled;
    private static bool _isHttpLoggingEnabled;
    private static LogLevel _httpOverridesLogLevel = LogLevel.Information;
    private static LogLevel _httpLoggingLogLevel = LogLevel.Information;

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
        ILogger? logger1 = null;

        ILogger EnsureLogger()
        {
            logger1 ??= _loggerLazy.Value!;
            return logger1 ??= NullLogger.Instance;
        }

        var healthCheckConfigurationSection = string.IsNullOrEmpty(healthCheckSectionName) ? configuration : configuration.GetSection(healthCheckSectionName);
        var healthCheckAppOptions = _healthCheckAppOptions = healthCheckConfigurationSection.Get<HealthCheckAppOptions>() ?? new HealthCheckAppOptions();

        if (healthCheckAppOptions.IsEnabled)
        {
            var logger = EnsureLogger();
            var logLevel = healthCheckAppOptions.LogLevel;
            logger.LogTrace("HealthCheck LogLevel={LogLevel}", logLevel);

            if (healthCheckConfigurationSection.GetChildren().Any())
            {
                logger.Log(logLevel, "Attempt to load HealthCheckOptions from configuration");
                services.Configure<HealthCheckOptions>(healthCheckConfigurationSection);
            }
            else
            {
                logger.Log(logLevel, "Add HealthChecks");
            }

            if (healthCheckAppOptions.RemoveResponseWriter)
            {
                logger.Log(logLevel, "HealthCheckOptions remove ResponseWriter");

                services.Configure<HealthCheckOptions>(options =>
                {
                    options.ResponseWriter = null!;
                });
            }
            else if (!string.IsNullOrEmpty(healthCheckAppOptions.Prefix) || !string.IsNullOrEmpty(healthCheckAppOptions.Suffix))
            {
                logger.Log(logLevel, "HealthCheckOptions customize Prefix:{Prefix} and Suffix:{Suffix}", healthCheckAppOptions.Prefix, healthCheckAppOptions.Suffix);

                var writer = HealthCheckResponseWriters.Instance = new HealthCheckResponseWriters(healthCheckAppOptions.Prefix, healthCheckAppOptions.Suffix);
                services.Configure<HealthCheckOptions>(options =>
                {
                    options.ResponseWriter = writer.WriteMinimalPlaintext;
                });
            }

            services.AddHealthChecks();     // Registers health checks services
        }

        _isForwardedHeadersEnabled = configuration[DefaultConfiguration.AspNetCoreForwardedHeadersEnabledKey].IsTrue();
        var httpOverridesConfigurationSection = string.IsNullOrEmpty(httpOverridesSectionName) ? configuration : configuration.GetSection(httpOverridesSectionName);
        _httpOverridesLogLevel = GetLogLevel(httpOverridesConfigurationSection, DefaultConfiguration.HttpOverridesLogLevelKey, LogLevel.Information);

        if (!_isForwardedHeadersEnabled)
        {
            var logger = EnsureLogger();

            logger.LogTrace("HttpOverrides LogLevel={LogLevel}", _httpOverridesLogLevel);

            logger.Log(_httpOverridesLogLevel, "Attempt to load ForwardedHeadersOptions from configuration");

            services.Configure<ForwardedHeadersOptions>(httpOverridesConfigurationSection);
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                if (httpOverridesConfigurationSection[DefaultConfiguration.ClearForwardLimitKey].IsTrue())
                {
                    options.ForwardLimit = null;
                }

                ProcessKnownProxies(httpOverridesConfigurationSection, options);

#if NET10_0_OR_GREATER
                ProcessKnownIPNetworks(httpOverridesConfigurationSection, options);
#else
                ProcessKnownNetworks(httpOverridesConfigurationSection, options);
#endif
            });
        }

        var httpLoggingEnabledKey = string.IsNullOrEmpty(httpLoggingSectionName) ? DefaultConfiguration.HttpLoggingEnabledKey : $"{httpLoggingSectionName}:Enabled";
        _isHttpLoggingEnabled = configuration[httpLoggingEnabledKey].IsTrue();
        var httpLoggingConfigurationSection = string.IsNullOrEmpty(httpLoggingSectionName) ? configuration : configuration.GetSection(httpLoggingSectionName);
        _httpLoggingLogLevel = GetLogLevel(httpLoggingConfigurationSection, DefaultConfiguration.HttpLoggingLogLevelKey);

        if (_isHttpLoggingEnabled)
        {
            var logger = EnsureLogger();

            logger.LogTrace("HttpLogging LogLevel={LogLevel}", _httpLoggingLogLevel);

            logger.Log(_httpLoggingLogLevel, "Attempt to load HttpLoggingOptions from configuration");

            services.Configure<HttpLoggingOptions>(httpLoggingConfigurationSection);
            var isClearRequestHeaders = httpLoggingConfigurationSection[DefaultConfiguration.ClearRequestHeadersKey].IsTrue();
            var isClearResponseHeaders = httpLoggingConfigurationSection[DefaultConfiguration.ClearResponseHeadersKey].IsTrue();
            var httpLoggingConfig = httpLoggingConfigurationSection.Get<HttpLoggingConfig>();
            var requestHeaders = httpLoggingConfig?.RequestHeaders.SplitSet() ?? [];
            var responseHeaders = httpLoggingConfig?.ResponseHeaders.SplitSet() ?? [];
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
        return InternalUseHttpOverrides(app, logger);
    }

    public static IApplicationBuilder UseHttpOverrides(this IApplicationBuilder app, ILogger? logger = null)
    {
        return InternalUseHttpOverrides(app, logger);
    }

    private static TApplication InternalUseHttpOverrides<TApplication>(TApplication app, ILogger? logger) where TApplication : IApplicationBuilder
    {
        logger ??= _loggerLazy.Value;
        logger ??= NullLogger.Instance;

        if (_healthCheckAppOptions == null)
        {
            logger.LogCritical("Run builder.AddHttpOverrides() first");
            throw new ApplicationException("Run builder.AddHttpOverrides() first");
        }

        var sp = app.ApplicationServices;
        var optionsForwardedHeadersOptions = sp.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var fho = optionsForwardedHeadersOptions.Value;

        if (_healthCheckAppOptions.IsEnabled)
        {
            var logLevel = _healthCheckAppOptions.LogLevel;
            if (_healthCheckAppOptions.IsAzureAppServiceContainer)
            {
                var mainHealthChecksPath = string.IsNullOrEmpty(_healthCheckAppOptions.Path) ? DefaultConfiguration.HealthChecksPath : _healthCheckAppOptions.Path;
                _healthCheckAppOptions.Paths = new[] { mainHealthChecksPath, DefaultConfiguration.HealthChecksAzureAppServiceContainer };
            }

            // Cannot use app.MapHealthChecks because of HttpsRedirection
            var port = _healthCheckAppOptions.Port;
            if (_healthCheckAppOptions.Paths is { } pathArrays && pathArrays.Length > 0)
            {
                var paths = pathArrays.Select(p => (PathString)p).ToArray();
                logger.Log(logLevel, "Use HealthChecks Port:{Port} Paths:{Paths}", port, paths);

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
                var healthChecksPath = _healthCheckAppOptions.Path;
                logger.Log(logLevel, "Use HealthChecks Port:{Port} Path:{Path}", port, healthChecksPath);
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
            logger.Log(_httpOverridesLogLevel, "HostFiltering: {@HostFiltering}", hostFiltering);
        }

        if (_isForwardedHeadersEnabled)
        {
            var bypassNetLahHttpOverridesMessage = $"Bypass HttpOverrides configuration settings because {DefaultConfiguration.AspNetCoreForwardedHeadersEnabledKey} is True";
            logger.Log(_httpOverridesLogLevel, bypassNetLahHttpOverridesMessage);
        }

#if NET10_0_OR_GREATER
        if (fho.KnownProxies.Count > 0 || fho.KnownIPNetworks.Count > 0 || fho.ForwardedHeaders != ForwardedHeaders.None)
        {
            logger.Log(_httpOverridesLogLevel, "ForwardLimit: {ForwardLimit}", fho.ForwardLimit);
        }
#else
        if (fho.KnownProxies.Count > 0 || fho.KnownNetworks.Count > 0 || fho.ForwardedHeaders != ForwardedHeaders.None)
        {
            logger.Log(_httpOverridesLogLevel, "ForwardLimit: {ForwardLimit}", fho.ForwardLimit);
        }
#endif

        if (fho.KnownProxies.Count > 0)
        {
            logger.Log(_httpOverridesLogLevel, "KnownProxies: {KnownProxies}", fho.KnownProxies.ToStringComma());
        }

#if NET10_0_OR_GREATER
        if (fho.KnownIPNetworks.Count > 0)
        {
            logger.Log(_httpOverridesLogLevel, "KnownIPNetworks: {KnownIPNetworks}", fho.KnownIPNetworks.ToStringComma());
        }
#else
        if (fho.KnownNetworks.Count > 0)
        {
            logger.Log(_httpOverridesLogLevel, "KnownNetworks: {KnownNetworks}", fho.KnownNetworks.ToStringComma());
        }
#endif

        if (fho.ForwardedHeaders != ForwardedHeaders.None)
        {
            var forwardedHeaders = string.Join(",", fho.ForwardedHeaders);
            if (_isForwardedHeadersEnabled)
            {
                logger.Log(_httpOverridesLogLevel, "ForwardedHeaders: {ForwardedHeaders}", forwardedHeaders);
            }
            else
            {
                app.UseForwardedHeaders();
                logger.Log(_httpOverridesLogLevel, "Use ForwardedHeaders: {ForwardedHeaders}", forwardedHeaders);
            }
        }

        if (fho.AllowedHosts.Count > 0)
        {
            var allowedHosts = string.Join(",", fho.AllowedHosts);
            logger.Log(_httpOverridesLogLevel, "AllowedHosts: {AllowedHosts}", allowedHosts);
        }

        if (_isHttpLoggingEnabled)
        {
            var httpLogging = sp.GetRequiredService<IOptions<HttpLoggingOptions>>()?.Value;
            logger.Log(_httpLoggingLogLevel, "Use HttpLogging LoggingFields:{LoggingFields} MediaTypeOptions:{MediaTypeOptions} RequestBodyLogLimit:{RequestBodyLogLimit} ResponseBodyLogLimit:{ResponseBodyLogLimit} RequestHeaders:{RequestHeaders} ResponseHeaders:{ResponseHeaders}",
                httpLogging?.LoggingFields, httpLogging?.MediaTypeOptions, httpLogging?.RequestBodyLogLimit, httpLogging?.ResponseBodyLogLimit,
                string.Join(',', httpLogging?.RequestHeaders ?? Enumerable.Empty<string>()),
                string.Join(',', httpLogging?.ResponseHeaders ?? Enumerable.Empty<string>()));
            app.UseHttpLogging();
        }

        return app;
    }

#if NET10_0_OR_GREATER
    private static void ProcessKnownIPNetworks(IConfiguration configuration, ForwardedHeadersOptions options)
    {
        var knownNetworks = configuration[DefaultConfiguration.KnownIPNetworksKey] ?? configuration[DefaultConfiguration.KnownNetworksKey];
        if (knownNetworks != null || configuration[DefaultConfiguration.ClearKnownIPNetworksKey].IsTrue() || configuration[DefaultConfiguration.ClearKnownNetworksKey].IsTrue())
        {
            options.KnownIPNetworks.Clear();
        }

        foreach (var item in knownNetworks.SplitSet())
        {
            var net = item.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (net.Length == 2)
            {
                var prefix = System.Net.IPAddress.Parse(net[0]);
                var prefixLength = int.Parse(net[1]);
                options.KnownIPNetworks.Add(new IPNetwork(prefix, prefixLength));
            }
            else if (net.Length == 1)
            {
                var prefix = System.Net.IPAddress.Parse(net[0]);
                var prefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
                options.KnownIPNetworks.Add(new IPNetwork(prefix, prefixLength));
            }
        }
    }
#else
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
#endif

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

    private class HttpLoggingConfig
    {
        public string? RequestHeaders { get; set; }
        public string? ResponseHeaders { get; set; }
        public List<string>? MediaTypeOptions { get; set; }
    }

    internal static LogLevel GetLogLevel(IConfiguration configuration, string key, LogLevel defaultLogLevel = LogLevel.Debug)
    {
        return configuration.GetValue<LogLevel?>(key) ?? defaultLogLevel;
    }
}
