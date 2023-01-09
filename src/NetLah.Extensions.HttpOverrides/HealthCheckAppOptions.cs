namespace NetLah.Extensions.HttpOverrides;

internal class HealthCheckAppOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool IsAzureAppServiceContainer { get; set; }

    public string Path { get; set; } = DefaultConfiguration.HealthChecksPath;

    public string[]? Paths { get; set; }

    public int? Port { get; set; }
}
