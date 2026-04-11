using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RncPlatform.Infrastructure;
using StackExchange.Redis;

namespace RncPlatform.Api.Tests;

public class ValkeyConfigurationTests
{
    [Fact]
    public void AddInfrastructure_ShouldConvertRedissUriSecretToStackExchangeRedisConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=RncPlatformValkeyTests;Trusted_Connection=True;MultipleActiveResultSets=true",
                ["ConnectionStrings:Valkey"] = "rediss://red-d7c6gohkh4rs73cfcvkg:iMRjDSLhMMKRb8HjakzFCgMMT5qYZ5gr@virginia-keyvalue.render.com:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var cacheOptions = serviceProvider.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        var parsedOptions = ConfigurationOptions.Parse(cacheOptions.Configuration!);

        var endpoint = Assert.Single(parsedOptions.EndPoints);
        var dnsEndpoint = Assert.IsType<DnsEndPoint>(endpoint);

        Assert.Equal("virginia-keyvalue.render.com", dnsEndpoint.Host);
        Assert.Equal(6379, dnsEndpoint.Port);
        Assert.True(parsedOptions.Ssl);
        Assert.Equal("virginia-keyvalue.render.com", parsedOptions.SslHost);
        Assert.Equal("red-d7c6gohkh4rs73cfcvkg", parsedOptions.User);
        Assert.Equal("iMRjDSLhMMKRb8HjakzFCgMMT5qYZ5gr", parsedOptions.Password);
    }

    [Fact]
    public void AddInfrastructure_ShouldReadDatabaseFromRedisUriPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=RncPlatformValkeyTests;Trusted_Connection=True;MultipleActiveResultSets=true",
                ["ConnectionStrings:Valkey"] = "redis://default:secret@cache.internal:6379/3"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var cacheOptions = serviceProvider.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        var parsedOptions = ConfigurationOptions.Parse(cacheOptions.Configuration!);

        var endpoint = Assert.Single(parsedOptions.EndPoints);
        var dnsEndpoint = Assert.IsType<DnsEndPoint>(endpoint);

        Assert.Equal("cache.internal", dnsEndpoint.Host);
        Assert.Equal(6379, dnsEndpoint.Port);
        Assert.False(parsedOptions.Ssl);
        Assert.Equal("default", parsedOptions.User);
        Assert.Equal("secret", parsedOptions.Password);
        Assert.Equal(3, parsedOptions.DefaultDatabase);
    }
}