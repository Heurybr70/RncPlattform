using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace RncPlatform.Api.Tests.Infrastructure;

public sealed class RncApiTestContext : IAsyncDisposable
{
    private RncApiTestContext(RncApiTestFactory factory, HttpClient client)
    {
        Factory = factory;
        Client = client;
    }

    public RncApiTestFactory Factory { get; }

    public HttpClient Client { get; }

    public static async Task<RncApiTestContext> CreateAsync()
    {
        var factory = new RncApiTestFactory();
        await factory.InitializeAsync();
        var client = factory.CreateApiClient();
        return new RncApiTestContext(factory, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.CleanupAsync();
        Factory.Dispose();
    }
}