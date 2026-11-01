using System.Text.Json;
using StackExchange.Redis;

namespace BankApi.Integrations.Caching;

/// <summary>
/// Petit wrapper autour de Redis pour deux usages :
///  1) cache "cache-aside" (GetOrSetAsync) : on lit Redis d'abord, sinon on calcule et on stocke avec un TTL ;
///  2) stockage de jetons temporaires (connexion 2FA en attente) avec expiration automatique.
/// Si Redis est indisponible, le cache est simplement ignoré (l'application continue de fonctionner,
/// juste sans cache) — un cache ne doit jamais faire tomber l'application.
/// </summary>
public class RedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(key);

            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString());
                if (value is not null)
                {
                    _logger.LogDebug("Cache HIT : {Key}", key);
                    return value;
                }
            }

            _logger.LogDebug("Cache MISS : {Key}", key);
            var fresh = await factory();
            await db.StringSetAsync(key, JsonSerializer.Serialize(fresh), ttl);
            return fresh;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning("Redis indisponible, cache ignoré : {Message}", ex.Message);
            return await factory();
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning("Redis indisponible, invalidation ignorée : {Message}", ex.Message);
        }
    }

    // --- Jetons de connexion 2FA en attente (valides 5 minutes) ---

    private static string PendingKey(string token) => $"pending2fa:{token}";

    public Task StorePendingLoginAsync(string token, Guid customerId, TimeSpan ttl) =>
        _redis.GetDatabase().StringSetAsync(PendingKey(token), customerId.ToString(), ttl);

    public async Task<Guid?> GetPendingLoginAsync(string token)
    {
        var value = await _redis.GetDatabase().StringGetAsync(PendingKey(token));
        return value.HasValue && Guid.TryParse(value.ToString(), out var id) ? id : null;
    }

    public Task RemovePendingLoginAsync(string token) =>
        _redis.GetDatabase().KeyDeleteAsync(PendingKey(token));
}
