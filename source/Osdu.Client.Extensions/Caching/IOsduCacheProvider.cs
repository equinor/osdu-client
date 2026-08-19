using System.Linq.Expressions;

namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// Provides access to strongly-typed OSDU reference data caches.
/// Inject this interface to retrieve cached data or resolve individual cache instances.
/// </summary>
public interface IOsduCacheProvider
{
    /// <summary>
    /// Gets all cached items for the given type in a single call.
    /// </summary>
    Task<List<TItem>> GetAllAsync<TItem>(CancellationToken ct = default);

    /// <summary>
    /// Gets cached items matching a raw Lucene query string.
    /// </summary>
    Task<List<TItem>> GetByQueryAsync<TItem>(string query, CancellationToken ct = default);

    /// <summary>
    /// Gets cached items matching a strongly-typed predicate expression.
    /// </summary>
    Task<List<TItem>> GetByQueryAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Resolves the underlying cache instance for advanced operations
    /// (e.g. invalidation, accessing <see cref="CachedResult{T}"/> metadata).
    /// </summary>
    OsduCache<TItem> For<TItem>();

    /// <summary>
    /// Registers a new cache descriptor at runtime.
    /// </summary>
    void Register(OsduCacheDescriptor descriptor);

    /// <summary>
    /// Registers a new cache descriptor at runtime using a fluent API.
    /// </summary>
    void Register<TItem>(string kind, Action<CacheOptions>? configure = null);

    /// <summary>
    /// Returns all registered item types.
    /// </summary>
    IReadOnlyCollection<Type> RegisteredTypes { get; }
}
