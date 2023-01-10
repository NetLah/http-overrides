using Microsoft.Extensions.Logging;

namespace NetLah.Extensions.HttpOverrides;

internal class HealthCheckAppOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool IsAzureAppServiceContainer { get; set; }

    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    public string? Path { get; set; } = DefaultConfiguration.HealthChecksPath;

    public string[]? Paths { get; set; }

    public int? Port { get; set; }

    public string? Prefix { get; set; }

    public string? Suffix { get; set; }

    public bool RemoveResponseWriter { get; set; }
}
