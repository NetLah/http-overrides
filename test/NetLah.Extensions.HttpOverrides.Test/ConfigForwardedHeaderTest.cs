using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NetLah.Extensions.HttpOverrides.Test;

public class ConfigForwardedHeaderTest
{
    private static IConfiguration NewConfig(Dictionary<string, string?> initialData)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

    [Fact]
    public void DefaultForwardedHeaderOptionsTest()
    {
        var configuration = NewConfig(new Dictionary<string, string?>());
        var services = new ServiceCollection();
        HttpOverridesExtensions.AddHttpOverrides(services, configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal("X-Forwarded-For", options.ForwardedForHeaderName);
        Assert.Equal("X-Forwarded-Host", options.ForwardedHostHeaderName);
        Assert.Equal("X-Forwarded-Proto", options.ForwardedProtoHeaderName);
        Assert.Equal("X-Original-For", options.OriginalForHeaderName);
        Assert.Equal("X-Original-Host", options.OriginalHostHeaderName);
        Assert.Equal("X-Original-Proto", options.OriginalProtoHeaderName);
        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal("::1", string.Join(",", options.KnownProxies));
        Assert.Equal("127.0.0.1/8", string.Join(",", options.KnownNetworks.Select(ipn => $"{ipn.Prefix}/{ipn.PrefixLength}")));
        Assert.Equal("", string.Join(",", options.AllowedHosts));
        Assert.False(options.RequireHeaderSymmetry);
    }

    [Fact]
    public void ConfigureForwardedHeaderOptionsTest()
    {
        var configuration = NewConfig(new Dictionary<string, string?>
        {
            ["HttpOverrides:ForwardedForHeaderName"] = "X-Forwarded-For_1",
            ["HttpOverrides:ForwardedHostHeaderName"] = "X-Forwarded-Host_2",
            ["HttpOverrides:ForwardedProtoHeaderName"] = "X-Forwarded-Proto_3",
            ["HttpOverrides:OriginalForHeaderName"] = "X-Original-For_4",
            ["HttpOverrides:OriginalHostHeaderName"] = "X-Original-Host_5",
            ["HttpOverrides:OriginalProtoHeaderName"] = "X-Original-Proto_6",
            ["HttpOverrides:ForwardedHeaders"] = "XForwardedProto,XForwardedHost",
            ["HttpOverrides:ForwardLimit"] = "3",
            ["HttpOverrides:KnownProxies"] = "192.168.1.1,10.2.3.4",
            ["HttpOverrides:KnownNetworks"] = "172.16.0.0/12,100.64.0.0/10",
            ["HttpOverrides:AllowedHosts:0"] = "host1",
            ["HttpOverrides:AllowedHosts:1"] = "host2.example.com",
            ["HttpOverrides:AllowedHosts:2"] = "demo.example.com",
            ["HttpOverrides:RequireHeaderSymmetry"] = "true",
        });
        var services = new ServiceCollection();
        HttpOverridesExtensions.AddHttpOverrides(services, configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal("X-Forwarded-For_1", options.ForwardedForHeaderName);
        Assert.Equal("X-Forwarded-Host_2", options.ForwardedHostHeaderName);
        Assert.Equal("X-Forwarded-Proto_3", options.ForwardedProtoHeaderName);
        Assert.Equal("X-Original-For_4", options.OriginalForHeaderName);
        Assert.Equal("X-Original-Host_5", options.OriginalHostHeaderName);
        Assert.Equal("X-Original-Proto_6", options.OriginalProtoHeaderName);
        Assert.Equal(ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(3, options.ForwardLimit);
        Assert.Equal("192.168.1.1,10.2.3.4", string.Join(",", options.KnownProxies));
        Assert.Equal("172.16.0.0/12,100.64.0.0/10", string.Join(",", options.KnownNetworks.Select(ipn => $"{ipn.Prefix}/{ipn.PrefixLength}")));
        Assert.Equal("host1,host2.example.com,demo.example.com", string.Join(",", options.AllowedHosts));
        Assert.True(options.RequireHeaderSymmetry);
    }

}
