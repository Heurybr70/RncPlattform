using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RncPlatform.Application.Abstractions.Caching;

namespace RncPlatform.Infrastructure.Caching;

public class RncCacheService : IRncCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RncCacheService> _logger;

    public RncCacheService(IDistributedCache cache, ILogger<RncCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            if (bytes == null) return default;
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar obtener datos del caché para la llave: {Key}", key);
            return default; // Fallback al proveedor de datos original (DB)
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar guardar datos en el caché para la llave: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar eliminar llave del caché: {Key}", key);
        }
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
