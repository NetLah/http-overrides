using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NetLah.Extensions.HttpOverrides.Test;

public class ForwardedHeaderTest
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

}
