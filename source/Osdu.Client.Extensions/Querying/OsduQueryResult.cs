namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Result of an OSDU query execution.
/// </summary>
public class OsduQueryResult<TItem>
{
    public required List<TItem> Items { get; init; }
    public long TotalCount { get; init; }
    public bool IsComplete { get; init; }
    public required string Query { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
}
