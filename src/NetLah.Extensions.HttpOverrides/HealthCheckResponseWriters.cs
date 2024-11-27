using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;

namespace NetLah.Extensions.HttpOverrides;

// https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/HealthChecks/src/HealthCheckResponseWriters.cs
internal class HealthCheckResponseWriters
{
    public static HealthCheckResponseWriters Instance { get; set; } = new(default, default);

    private readonly byte[] DegradedBytes;
    private readonly byte[] HealthyBytes;
    private readonly byte[] UnhealthyBytes;

    public HealthCheckResponseWriters(string? prefix, string? suffix)
    {
        string Build(HealthStatus status)
        {
            return $"{prefix}{status}{suffix}";
        }

        DegradedBytes = Encoding.UTF8.GetBytes(Build(HealthStatus.Degraded));
        HealthyBytes = Encoding.UTF8.GetBytes(Build(HealthStatus.Healthy));
        UnhealthyBytes = Encoding.UTF8.GetBytes(Build(HealthStatus.Unhealthy));
    }

    public Task WriteMinimalPlaintext(HttpContext httpContext, HealthReport result)
    {
        httpContext.Response.ContentType = "text/plain";
        return result.Status switch
        {
            HealthStatus.Degraded => httpContext.Response.Body.WriteAsync(DegradedBytes.AsMemory()).AsTask(),
            HealthStatus.Healthy => httpContext.Response.Body.WriteAsync(HealthyBytes.AsMemory()).AsTask(),
            HealthStatus.Unhealthy => httpContext.Response.Body.WriteAsync(UnhealthyBytes.AsMemory()).AsTask(),
            _ => httpContext.Response.WriteAsync(result.Status.ToString())
        };
    }
}
