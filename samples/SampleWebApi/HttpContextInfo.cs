namespace SampleWebApi;

public class HttpContextInfo
{
    private readonly HttpContext _httpContext;
    private readonly Lazy<HostAndPort> _getHostAndPort;

    public HttpContextInfo(IHttpContextAccessor httpContextAccessor)
    {
        _httpContext = httpContextAccessor?.HttpContext ?? throw new ArgumentNullException(nameof(httpContextAccessor), "HttpContext is required");
        _getHostAndPort = new Lazy<HostAndPort>(GetHostAndPort);
    }

    private HostAndPort GetHostAndPort()
    {
        var request = _httpContext.Request;
        var hostMayWithPort = request.Host;
        var host = hostMayWithPort.Host;
        if (hostMayWithPort.Port is { } port)
        {
            return new HostAndPort(host, port);
        }

        if (request.Headers["X-FORWARDED-PORT"] is { } headerValues &&
            headerValues.FirstOrDefault() is { } headerValue &&
            int.TryParse(headerValue, out var portValue2))
        {
            return new HostAndPort(host, portValue2);
        }
        else
        {
            var connection = _httpContext.Connection;
            var localPort = connection.LocalPort;
            var unexpectedPort = request.Scheme == "https" ? 80 : 443;
            var defaultPortScheme = request.Scheme == "https" ? 443 : 80;
            var portValue3 = localPort == unexpectedPort ? defaultPortScheme : localPort;
            return new HostAndPort(host, portValue3);
        }
    }

    public string Host => _getHostAndPort.Value.Host;
    public int Port => _getHostAndPort.Value.Port;

    private sealed class HostAndPort
    {
        public HostAndPort(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }
    }
}
