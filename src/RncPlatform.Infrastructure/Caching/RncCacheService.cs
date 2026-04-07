using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using RncPlatform.Application.Abstractions.Caching;

namespace RncPlatform.Infrastructure.Caching;

public class RncCacheService : IRncCacheService
{
    private readonly IDistributedCache _cache;

    public RncCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes == null) return default;
        return JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await _cache.SetAsync(key, bytes, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // En IDistributedCache no existe remove por prefijo directo (requiere StackExchange.Redis native commands).
        // Se considerará la invalidación manual por llave particular o el borrado vía IServer en Redis directamente en escenario real.
        return Task.CompletedTask;
    }
}
