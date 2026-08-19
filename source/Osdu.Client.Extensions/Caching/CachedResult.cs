namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// Represents the result of a cached OSDU query with metadata.
/// </summary>
public class CachedResult<T>
{
    public required List<T> Items { get; init; }
    public long TotalCount { get; init; }
    public bool IsComplete { get; init; }
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
}

///// <summary>
///// A cached result containing raw JSON elements (for generic record browsing).
///// </summary>
//public class CachedRecordResult : CachedResult<JsonElement>;
