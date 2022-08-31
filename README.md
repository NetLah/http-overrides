# NetLah.Extensions.HttpOverrides - .NET Library

[NetLah.Extensions.HttpOverrides](https://www.nuget.org/packages/NetLah.Extensions.HttpOverrides/) is a library support setting ASP.NET Core HttpOverrides from configuration.

## Nuget package

[![NuGet](https://img.shields.io/nuget/v/NetLah.Extensions.HttpOverrides.svg?style=flat-square&label=nuget&colorB=00b200)](https://www.nuget.org/packages/NetLah.Extensions.HttpOverrides/)

## Build Status

[![Build Status](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Factions-badge.atrox.dev%2FNetLah%2Fhttp-overrides%2Fbadge%3Fref%3Dmain&style=flat)](https://actions-badge.atrox.dev/NetLah/http-overrides/goto?ref=main)

## Getting started

### 1. Add/Update PackageReference to .csproj

```xml
<ItemGroup>
  <PackageReference Include="NetLah.Extensions.HttpOverrides" Version="0.2.0" />
</ItemGroup>
```

### 2. Settings From Configuration

```csharp
builder.Services.AddHttpOverrides(builder.Configuration);
```

### 3. Applies Http Overrides

```csharp
app.UseHttpOverrides(logger);
```

## Overrided by ASPNETCORE_FORWARDEDHEADERS_ENABLED

This HttpOverrides will check configuration ASPNETCORE_FORWARDEDHEADERS_ENABLED or ForwardedHeaders_Enabled not turned on to not override default behavior of ASP.NETCore.

Reference [ForwardedHeadersOptionsSetup.cs](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/ForwardedHeadersOptionsSetup.cs)

## Sample and default configuration

```json
{
  "HttpOverrides": {
    "ClearForwardLimit": false,
    "ClearKnownProxies": false,
    "ClearKnownNetworks": false,
    "ForwardLimit": 1,
    "KnownNetworks": "::1",
    "KnownNetworks": "127.0.0.1/8",
    "ForwardedForHeaderName": "X-Forwarded-For",
    "ForwardedHostHeaderName": "X-Forwarded-Host",
    "ForwardedProtoHeaderName": "X-Forwarded-Proto",
    "OriginalForHeaderName": "X-Original-For",
    "OriginalHostHeaderName": "X-Original-Host",
    "OriginalProtoHeaderName": "X-Original-Proto",
    "ForwardedHeaders": "", // XForwardedFor,XForwardedHost,XForwardedProto
    "AllowedHosts": "" // "*""
  }
}
```
