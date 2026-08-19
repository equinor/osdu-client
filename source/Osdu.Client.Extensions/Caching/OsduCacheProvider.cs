using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Extensions.Querying;

namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// Default implementation of <see cref="IOsduCacheProvider"/>.
/// Lazily creates and resolves <see cref="OsduCache{TItem}"/> instances by generic type.
/// Supports runtime registration of new cache descriptors.
/// </summary>
public class OsduCacheProvider : IOsduCacheProvider
{
    private readonly IMemoryCache _memoryCache;
    private readonly IOsduQueryExecutor _queryExecutor;
    private readonly ConcurrentDictionary<Type, OsduCacheDescriptor> _descriptors = [];
    private readonly ConcurrentDictionary<Type, object> _caches = [];

    public OsduCacheProvider(IMemoryCache memoryCache, IOsduQueryExecutor queryExecutor, IEnumerable<OsduCacheDescriptor> descriptors)
    {
        _memoryCache = memoryCache;
        _queryExecutor = queryExecutor;

        foreach (var descriptor in descriptors)
            _descriptors[descriptor.ItemType] = descriptor;
    }

    /// <inheritdoc />
    public OsduCache<TItem> For<TItem>()
    {
        return (OsduCache<TItem>)_caches.GetOrAdd(typeof(TItem), type =>
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
                throw new InvalidOperationException(
                    $"No cache registered for type '{type.Name}'. Call Register<{type.Name}>() or add it via AddOsduCaching().");

            return new OsduCache<TItem>(_memoryCache, descriptor.Options, _queryExecutor, descriptor.KeyPrefix, descriptor.Kind);
        });
    }

    /// <inheritdoc />
    public async Task<List<TItem>> GetAllAsync<TItem>(CancellationToken ct = default)
    {
        var result = await For<TItem>().GetAllAsync(ct);
        return result.Items;
    }

    /// <inheritdoc />
    public async Task<List<TItem>> GetByQueryAsync<TItem>(string query, CancellationToken ct = default)
    {
        var result = await For<TItem>().GetByQueryAsync(query, ct);
        return result.Items;
    }

    /// <inheritdoc />
    public async Task<List<TItem>> GetByQueryAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken ct = default)
    {
        var result = await For<TItem>().GetByQueryAsync(predicate, ct);
        return result.Items;
    }

    /// <inheritdoc />
    public void Register(OsduCacheDescriptor descriptor)
    {
        _descriptors[descriptor.ItemType] = descriptor;
        _caches.TryRemove(descriptor.ItemType, out _);
    }

    /// <inheritdoc />
    public void Register<TItem>(string kind, Action<CacheOptions>? configure = null)
    {
        var options = new CacheOptions();
        configure?.Invoke(options);

        Register(new OsduCacheDescriptor
        {
            Kind = kind,
            Options = options,
            ItemType = typeof(TItem)
        });
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Type> RegisteredTypes => _descriptors.Keys.ToList().AsReadOnly();
}
