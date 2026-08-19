namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Options for controlling query execution behavior.
/// </summary>
public class OsduQueryOptions
{
    /// <summary>
    /// Number of records per page. Defaults to 100.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of pages to fetch. Set to 0 for unlimited.
    /// </summary>
    public int MaxPages { get; set; } = 10;

    /// <summary>
    /// When true, fetches all available records regardless of MaxPages.
    /// </summary>
    public bool FetchAll { get; set; }

    /// <summary>
    /// The fields to project in the results. When empty, all fields are returned.
    /// </summary>
    public List<string> ReturnedFields { get; set; } = [];

    /// <summary>
    /// The fields to exclude from the results.
    /// </summary>
    public List<string> ExcludedFields { get; set; } = [];

    /// <summary>
    /// Sort criteria. When empty, results use OSDU default ordering.
    /// </summary>
    public List<OsduSortField>? Sort { get; set; }
}
