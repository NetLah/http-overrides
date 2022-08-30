using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class DiagnosticsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetInfo([FromServices] NetLah.Diagnostics.IAssemblyInfo appInfo)
    {
        try
        {
            return Ok($"AppTitle:{appInfo.Title}; Version:{appInfo.InformationalVersion} BuildTime:{appInfo.BuildTimestampLocal}; Framework:{appInfo.FrameworkName}; TimeZoneInfo.Local:{TimeZoneInfo.Local.DisplayName} / {TimeZoneInfo.Local.BaseUtcOffset}");
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                Success = false,
                Error = ex.Message,
                Detail = ex.ToString(),
            });
        }
    }

    [HttpGet]
    public IActionResult Connection([FromServices] HttpContextInfo httpContextInfo, string? endpoint)
    {
        try
        {
            var request = HttpContext.Request;
            var remote = HttpContext.Connection;
            var remoteIpAddress = remote.RemoteIpAddress?.ToString();
            if (remoteIpAddress?.Contains(':') == true)
            {
                remoteIpAddress = $"[{remoteIpAddress}]";
            }
            var endpointInfo = string.IsNullOrEmpty(endpoint) ? null : $"[{endpoint}]";
            var connectionInfo = $"Server{endpointInfo}:{request.Scheme}://{httpContextInfo.Host}:{httpContextInfo.Port} Client:{remoteIpAddress}:{remote?.RemotePort}";
            return Ok(connectionInfo);
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                Success = false,
                Error = ex.Message,
                Detail = ex.ToString(),
            });
        }
    }

    // Add multi connections query
    [HttpGet] public IActionResult Connection1([FromServices] HttpContextInfo httpContextInfo) => Connection(httpContextInfo, "Connection1");
    [HttpGet] public IActionResult Connection2([FromServices] HttpContextInfo httpContextInfo) => Connection(httpContextInfo, "Connection2");
    [HttpGet] public IActionResult Connection3([FromServices] HttpContextInfo httpContextInfo) => Connection(httpContextInfo, "Connection3");
}
