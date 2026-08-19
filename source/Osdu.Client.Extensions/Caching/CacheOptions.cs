namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// Configuration options for a named OSDU reference data cache.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Absolute expiration relative to the time the entry is created.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Number of records per page when fetching from OSDU Search API.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of pages to fetch. Set to 0 for unlimited (fetch all).
    /// </summary>
    public int MaxPages { get; set; } = 10;

    /// <summary>
    /// When true, fetches all available records regardless of MaxPages.
    /// </summary>
    public bool CacheAll { get; set; }
}
