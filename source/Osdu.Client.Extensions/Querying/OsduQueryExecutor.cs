using System.Linq.Expressions;
using Osdu.Client.Apis.Search;
using Osdu.Client.Extensions.Extensions;

namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Default implementation of <see cref="IOsduQueryExecutor"/>.
/// Executes paginated OSDU Search queries with strongly-typed deserialization.
/// </summary>
public class OsduQueryExecutor(IOsduClient osduClient) : IOsduQueryExecutor
{
    /// <inheritdoc />
    public OsduQuery<TItem> Query<TItem>(string kind) => new(this, kind);

    /// <inheritdoc />
    public Task<OsduQueryResult<TItem>> ExecuteAsync<TItem>(
        string kind,
        Expression<Func<TItem, bool>> predicate,
        OsduQueryOptions? options = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(predicate);
        return ExecuteAsync<TItem>(kind, query, options, ct);
    }

    /// <inheritdoc />
    public Task<OsduQueryResult<TItem>> ExecuteAsync<TItem>(
        string kind,
        string query,
        OsduQueryOptions? options = null,
        CancellationToken ct = default)
    {
        return FetchPaginatedAsync<TItem>(kind, query, options ?? new OsduQueryOptions(), ct);
    }

    /// <inheritdoc />
    public Task<OsduQueryResult<TItem>> ExecuteAllAsync<TItem>(
        string kind,
        OsduQueryOptions? options = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync<TItem>(kind, "*", options, ct);
    }

    /// <inheritdoc />
    public string BuildQuery<TItem>(Expression<Func<TItem, bool>> predicate)
    {
        return OsduQueryBuilder.Build(predicate);
    }

    /// <inheritdoc />
    public string ResolveField<TItem>(Expression<Func<TItem, object?>> selector)
    {
        return OsduFieldSelector.Resolve(selector);
    }

    /// <inheritdoc />
    public List<string> ResolveFields<TItem>(params Expression<Func<TItem, object?>>[] selectors)
    {
        return OsduFieldSelector.ResolveMany(selectors);
    }

    private async Task<OsduQueryResult<TItem>> FetchPaginatedAsync<TItem>(
        string kind, string query, OsduQueryOptions options, CancellationToken ct)
    {
        var allItems = new List<TItem>();
        string? cursor = null;
        var pagesFetched = 0;
        long totalCount = 0;
        var isComplete = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!options.FetchAll && options.MaxPages > 0 && pagesFetched >= options.MaxPages)
                break;

            var request = new CursorQueryRequest
            {
                Kind = kind,
                Query = query,
                Limit = options.PageSize,
                Cursor = cursor,
                TrackTotalCount = pagesFetched == 0
            };

            if (options.ReturnedFields is { Count: > 0 })
                request.ReturnedFields = options.ReturnedFields;

            if (options.ExcludedFields is { Count: > 0 })
                request.ExcludedFields = options.ExcludedFields;

            var response = await osduClient.Search.PostQueryWithCursorAsync(request, cancellationToken: ct);

            if (pagesFetched == 0 && response?.TotalCount is > 0)
                totalCount = response.TotalCount.Value;

            if (response?.Results is null || response.Results.Count == 0)
            {
                isComplete = true;
                break;
            }

            allItems.AddRange(response.Results.Deserialize<TItem>());

            pagesFetched++;
            cursor = response.Cursor;

            if (string.IsNullOrEmpty(cursor))
            {
                isComplete = true;
                break;
            }
        }

        return new OsduQueryResult<TItem>
        {
            Items = allItems,
            TotalCount = totalCount > 0 ? totalCount : allItems.Count,
            IsComplete = isComplete,
            Query = query,
            ExecutedAt = DateTimeOffset.UtcNow
        };
    }
}
