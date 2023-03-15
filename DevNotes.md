# Development Notes

## Overview
- Notes on build and Github actions

## .NET 8.0 Build issues

- C# 11.0,  CS8936: Feature 'file types' is not available in C# 10.0

```
/Users/runner/work/http-overrides/http-overrides/src/NetLah.Extensions.HttpOverrides/Microsoft.AspNetCore.Http.Generators
    /Microsoft.AspNetCore.Http.Generators.RequestDelegateGenerator/GeneratedRouteBuilderExtensions.g.cs(52,23): 
    error CS8936: Feature 'file types' is not available in C# 10.0. Please use language version 11.0 or greater. 
    [/Users/runner/work/http-overrides/http-overrides/src/NetLah.Extensions.HttpOverrides/NetLah.Extensions.HttpOverrides.csproj::TargetFramework=net8.0]
```