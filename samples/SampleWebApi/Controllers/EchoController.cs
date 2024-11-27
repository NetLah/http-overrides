using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace SampleWebApi.Controllers;

[Route("e")]
[ApiExplorerSettings(IgnoreApi = true)]
public class EchoController : ControllerBase
{
    [Route("{*url}")]
    public async Task<ActionResult<MyResult>> Action(string url)
    {
        var request = HttpContext.Request;
        var result = new MyResult
        {
            Url = url,
            Method = request.Method,
            Scheme = request.Scheme,
            Host = request.Host.ToString(),
            Connection = new ConnInfo(HttpContext.Connection),
            PathBase = request.PathBase.ToString(),
            Path = request.Path.ToString(),
            ContentType = request.ContentType,
            QueryString = request.QueryString.ToString(),
            Headers = ToDict(request.Headers),
            Form = ToDict(request.HasFormContentType ? request.Form : null),
            Query = ToDict(request.Query),
        };

        if (!string.IsNullOrEmpty(request.ContentType))
        {
            using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            result.Body = await reader.ReadToEndAsync();
        }

        return result;
    }

    private static IDictionary<string, string>? ToDict(IEnumerable<KeyValuePair<string, StringValues>>? headers)
    {
        return headers?.ToDictionary(e => e.Key, e => e.Value.ToString());
    }

    public class MyResult
    {
        public string? Url { get; set; }
        public string? Body { get; set; }
        public string? Method { get; set; }
        public string? Scheme { get; set; }
        public string? Host { get; set; }
        public ConnInfo? Connection { get; set; }
        public string? PathBase { get; set; }
        public string? Path { get; set; }
        public string? ContentType { get; set; }
        public string? QueryString { get; set; }
        public IDictionary<string, string>? Headers { get; set; }
        public IDictionary<string, string>? Form { get; set; }
        public IDictionary<string, string>? Query { get; set; }
    }

    public class ConnInfo
    {
        public ConnInfo(ConnectionInfo connectionInfo)
        {
            if (connectionInfo != null)
            {
                Id = connectionInfo.Id;
                LocalIpAddress = connectionInfo.LocalIpAddress?.ToString();
                LocalPort = connectionInfo.LocalPort;
                RemoteIpAddress = connectionInfo.RemoteIpAddress?.ToString();
                RemotePort = connectionInfo.RemotePort;
            }
            else
            {
                Id = "Unknown";
            }
        }

        public string Id { get; }
        public string? LocalIpAddress { get; }
        public int LocalPort { get; }
        public string? RemoteIpAddress { get; }
        public int RemotePort { get; }
    }
}
