using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Extensions.Querying;

namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// A generic, reusable OSDU reference data cache. Delegates query execution
/// to <see cref="IOsduQueryExecutor"/> and caches the results.
/// </summary>
public class OsduCache<TItem>
{
    private readonly IMemoryCache _cache;
    private readonly IOsduQueryExecutor _queryExecutor;
    private readonly string _keyPrefix;
    private readonly string _kind;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = [];

    protected CacheOptions Options { get; }

    public OsduCache(IMemoryCache cache, CacheOptions options, IOsduQueryExecutor queryExecutor, string keyPrefix, string kind)
    {
        _cache = cache;
        Options = options;
        _queryExecutor = queryExecutor;
        _keyPrefix = keyPrefix;
        _kind = kind;
    }

    /// <summary>
    /// Gets all cached records for this kind, fetching from OSDU if not cached.
    /// </summary>
    public Task<CachedResult<TItem>> GetAllAsync(CancellationToken ct = default) =>
        GetOrCreateAsync("all", "*", ct);

    /// <summary>
    /// Gets cached records for a raw Lucene query string.
    /// </summary>
    public Task<CachedResult<TItem>> GetByQueryAsync(string query, CancellationToken ct = default) =>
        GetOrCreateAsync($"query:{query}", query, ct);

    /// <summary>
    /// Gets cached records using a strongly-typed predicate expression.
    /// </summary>
    public Task<CachedResult<TItem>> GetByQueryAsync(Expression<Func<TItem, bool>> predicate, CancellationToken ct = default)
    {
        var query = _queryExecutor.BuildQuery(predicate);
        return GetByQueryAsync(query, ct);
    }

    /// <summary>
    /// Invalidates a specific cached entry by key.
    /// </summary>
    public void Invalidate(string key) => _cache.Remove($"{_keyPrefix}:{key}");

    /// <summary>
    /// Invalidates the default "all" entry.
    /// </summary>
    public void InvalidateAll() => Invalidate("all");

    /// <summary>
    /// Invalidates all entries matching the given keys.
    /// </summary>
    public void Invalidate(params string[] keys)
    {
        foreach (var key in keys)
            _cache.Remove($"{_keyPrefix}:{key}");
    }

    private async Task<CachedResult<TItem>> GetOrCreateAsync(string cacheKey, string query, CancellationToken ct)
    {
        var fullKey = $"{_keyPrefix}:{cacheKey}";

        // Fast path: return cached result without locking
        if (_cache.TryGetValue(fullKey, out CachedResult<TItem>? cached) && cached is not null)
            return cached;

        // Slow path: acquire per-key lock to prevent thundering herd
        var keyLock = _locks.GetOrAdd(fullKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(fullKey, out cached) && cached is not null)
                return cached;

            var queryResult = await _queryExecutor.ExecuteAsync<TItem>(_kind, query, new OsduQueryOptions
            {
                PageSize = Options.PageSize,
                MaxPages = Options.MaxPages,
                FetchAll = Options.CacheAll
            }, ct);

            var result = new CachedResult<TItem>
            {
                Items = queryResult.Items,
                TotalCount = queryResult.TotalCount,
                IsComplete = queryResult.IsComplete,
                CachedAt = DateTimeOffset.UtcNow
            };

            _cache.Set(fullKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Options.Expiration
            });

            return result;
        }
        finally
        {
            keyLock.Release();
        }
    }
}
