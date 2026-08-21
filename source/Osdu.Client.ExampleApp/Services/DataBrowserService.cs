using System.Text.Json;
using Osdu.Client.Apis.Schema;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp.Services;

/// <summary>
/// Service for fetching OSDU kinds and records for the Data Browser.
/// </summary>
public class DataBrowserService(IOsduClient osduClient)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Fetches all available schema kinds grouped by entity type category.
    /// </summary>
    public async Task<List<KindGroup>> GetGroupedKindsAsync(CancellationToken ct = default)
    {
        var allSchemas = new List<SchemaInfo>();
        int offset = 0;
        const int limit = 100;

        while (true)
        {
            var response = await osduClient.Schema.GetSchemaAsync(
                limit: limit, offset: offset, cancellationToken: ct);

            if (response.SchemaInfos is null || response.SchemaInfos.Count == 0)
                break;

            allSchemas.AddRange(response.SchemaInfos);
            offset += response.SchemaInfos.Count;

            if (response.SchemaInfos.Count < limit)
                break;
        }

        return allSchemas
            .Select(s => new KindEntry(
                FormatKindId(s.SchemaIdentity),
                GetCategory(s.SchemaIdentity.EntityType),
                s.SchemaIdentity.EntityType))
            .GroupBy(k => k.Category)
            .OrderBy(g => g.Key)
            .Select(g => new KindGroup(g.Key, g.OrderBy(k => k.KindId).ToList()))
            .ToList();
    }

    /// <summary>
    /// Fetches a page of records for a given kind using cursor-based pagination.
    /// </summary>
    public async Task<SearchPage> SearchByKindAsync(
        string kind, int limit = 100, string? cursor = null, CancellationToken ct = default)
    {
        var response = await osduClient.Search.PostQueryWithCursorAsync(
            new CursorQueryRequest
            {
                Kind = kind,
                Limit = limit,
                Cursor = cursor
            }, cancellationToken: ct);

        var results = new List<JsonElement>();
        if (response.Results is not null)
        {
            foreach (var item in response.Results)
            {
                if (item is JsonElement je)
                    results.Add(je);
                else
                    results.Add(JsonSerializer.SerializeToElement(item, s_jsonOptions));
            }
        }

        return new SearchPage(results, response.TotalCount ?? 0, response.Cursor);
    }

    /// <summary>
    /// Fetches ALL records for a kind by following cursors to completion.
    /// </summary>
    public async Task<List<JsonElement>> FetchAllAsync(
        string kind, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var all = new List<JsonElement>();
        string? cursor = null;

        while (true)
        {
            var page = await SearchByKindAsync(kind, 1000, cursor, ct);
            all.AddRange(page.Results);
            progress?.Report(all.Count);

            if (string.IsNullOrEmpty(page.Cursor) || page.Results.Count == 0)
                break;

            cursor = page.Cursor;
        }

        return all;
    }

    private static string FormatKindId(SchemaIdentity id) =>
        $"{id.Authority}:{id.Source}:{id.EntityType}:{id.SchemaVersionMajor}.{id.SchemaVersionMinor}.{id.SchemaVersionPatch}";

    private static string GetCategory(string entityType)
    {
        return entityType.Contains("--")
            ? entityType[..entityType.IndexOf("--")]
            : "Custom";
    }
}

public record KindEntry(string KindId, string Category, string EntityType);
public record KindGroup(string Category, List<KindEntry> Kinds);
public record SearchPage(List<JsonElement> Results, long TotalCount, string? Cursor);