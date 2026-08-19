using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions.Querying;
using Osdu.Client.Schemas.MasterData;

namespace Osdu.Client.ExampleApp.Examples.Querying;

public class QueryPerformanceExample(IOsduQueryExecutor queryExecutor) : ExampleBase
{
    public override string Category => ExampleCategory.Querying;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => "Demonstrates IOsduQueryExecutor usage with timing statistics for fluent, predicate, and raw queries.";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of record to query (e.g. osdu:wks:master-data--Wellbore:1.3.0).")]
    public string Kind { get; set; } = "osdu:wks:master-data--Wellbore:1.2.0";

    [ExampleParameter(DisplayName = "Well ID Filter", Order = 1, Description = "WellID value to filter on (exact match).")]
    public string WellIdFilter { get; set; } = "dev:master-data--Well:Drogon-55-33-2";

    [ExampleParameter(DisplayName = "Wildcard Pattern", Order = 2, Description = "Wildcard pattern for WellID (e.g. 33-A).")]
    public string WildcardPattern { get; set; } = "33-A";

    [ExampleParameter(DisplayName = "Raw Query", Order = 3, Description = "Raw Lucene query string to execute.")]
    public string RawQuery { get; set; } = "data.WellID:*";

    [ExampleParameter(DisplayName = "Returned Fields", Order = 4, Description = "Comma-separated list of fields to return.")]
    public string[] ReturnedFields { get; set; } = ["id", "kind", "data.WellID"];

    [ExampleParameter(DisplayName = "Iterations", Order = 5, Description = "Number of times to repeat each query for averaging.")]
    public int Iterations { get; set; } = 3;

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var stats = new List<object>();
        var sw = new Stopwatch();

        // ==========================================================================
        // 1. Fluent query with Where, Select, OrderBy
        // ==========================================================================
        sw.Restart();
        var fluentResult = await queryExecutor
            .Query<Wellbore_1_2_0>(Kind)
            .Where(w => w.Data.WellID == WellIdFilter)
            .Select(w => w.Id, w => w.Kind, w => w.Data.WellID)
            .OrderBy(w => w.Data.WellID)
            .ExecuteAsync(cancellationToken);
        sw.Stop();
        stats.Add(new
        {
            Operation = @"Fluent query (Where + Select + OrderBy)",
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ResultCount = fluentResult.Items.Count,
            TotalCount = fluentResult.TotalCount,
            fluentResult.Query,
            fluentResult.IsComplete
        });

        // ==========================================================================
        // 2. Fluent query with wildcard pattern
        // ==========================================================================
        sw.Restart();
        var wildcardResult = await queryExecutor
            .Query<Wellbore_1_2_0>(Kind)
            .Where(w => w.Data.WellID.MatchesPattern(WildcardPattern))
            .Select(w => w.Id, w => w.Kind, w => w.Data.WellID)
            .ExecuteAsync(cancellationToken);
        sw.Stop();
        stats.Add(new
        {
            Operation = "Fluent query (MatchesPattern)",
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ResultCount = wildcardResult.Items.Count,
            TotalCount = wildcardResult.TotalCount,
            wildcardResult.Query,
            wildcardResult.IsComplete
        });

        // ==========================================================================
        // 3. Predicate-based query via ExecuteAsync
        // ==========================================================================
        sw.Restart();
        var predicateResult = await queryExecutor.ExecuteAsync<Wellbore_1_2_0>(Kind, w => w.Data.WellID == WellIdFilter, ct: cancellationToken);
        sw.Stop();
        stats.Add(new
        {
            Operation = "ExecuteAsync (predicate)",
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ResultCount = predicateResult.Items.Count,
            TotalCount = predicateResult.TotalCount,
            predicateResult.Query,
            predicateResult.IsComplete
        });

        // ==========================================================================
        // 4. Raw Lucene query
        // ==========================================================================
        sw.Restart();
        var rawResult = await queryExecutor.ExecuteAsync<Wellbore_1_2_0>(Kind, RawQuery, ct: cancellationToken);
        sw.Stop();
        stats.Add(new
        {
            Operation = "ExecuteAsync (raw query)",
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ResultCount = rawResult.Items.Count,
            TotalCount = rawResult.TotalCount,
            rawResult.Query,
            rawResult.IsComplete
        });

        // 5. Repeated fluent query to measure average latency
        var iterationTimes = new List<double>();
        for (int i = 0; i < Iterations; i++)
        {
            sw.Restart();
            await queryExecutor
                .Query<Wellbore_1_2_0>(Kind)
                .Where(w => w.Data.WellID == WellIdFilter)
                .Select(w => w.Id, w => w.Kind, w => w.Data.WellID)
                .ExecuteAsync(cancellationToken);
            sw.Stop();
            iterationTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        var result = new
        {
            QueryStatistics = stats,
            RepeatedFluentQuery = new
            {
                Iterations,
                IndividualMs = iterationTimes,
                AverageMs = iterationTimes.Count > 0 ? iterationTimes.Average() : 0,
                MinMs = iterationTimes.Count > 0 ? iterationTimes.Min() : 0,
                MaxMs = iterationTimes.Count > 0 ? iterationTimes.Max() : 0
            }
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
