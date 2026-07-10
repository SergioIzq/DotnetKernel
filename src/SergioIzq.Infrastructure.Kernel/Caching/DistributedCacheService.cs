using System.Text.Json;
using SergioIzq.Application.Kernel.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace SergioIzq.Infrastructure.Kernel.Caching;

/// <summary>
/// Implementación de <see cref="ICacheService"/> sobre <see cref="IDistributedCache"/> estándar,
/// abstrayendo el proveedor subyacente (memoria, Redis, SQL...).
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public DistributedCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var cachedValue = await _cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(cachedValue))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(cachedValue);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null)
    {
        if (value == null) return;

        var options = new DistributedCacheEntryOptions();

        if (slidingExpiration.HasValue)
        {
            options.SlidingExpiration = slidingExpiration.Value;
        }

        if (absoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
        }

        // Valor por defecto: 5 minutos de sliding expiration
        if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
        {
            options.SlidingExpiration = TimeSpan.FromMinutes(5);
        }

        var jsonValue = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, jsonValue, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        var value = await _cache.GetStringAsync(key);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// LIMITACIÓN: IDistributedCache estándar no soporta invalidación por patrón, así que este
    /// método no hace nada. Si se necesita de verdad, usar Redis con StackExchange.Redis directo,
    /// o confiar en el versionado de listas de los query handlers del kernel (las claves antiguas
    /// quedan huérfanas y expiran solas).
    /// </summary>
    public Task InvalidateByPatternAsync(string pattern)
    {
        return Task.CompletedTask;
    }
}
