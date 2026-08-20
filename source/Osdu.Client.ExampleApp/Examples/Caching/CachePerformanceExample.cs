using Osdu.Client.Data.ReferenceData;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions.Caching;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;


namespace Osdu.Client.ExampleApp.Examples.Caching;

public class CachePerformanceExample(IOsduCacheProvider cacheProvider) : ExampleBase
{
    public override string Category => ExampleCategory.Caching;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => "Demonstrates IOsduCacheProvider usage with timing statistics for each operation.";

    [ExampleParameter(DisplayName = "Iterations", Order = 0, Description = "Number of times to repeat each cache call for averaging.")]
    public int Iterations { get; set; } = 3;

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var sampleImageColours = await cacheProvider.GetAllAsync<SampleImageColourSpace_1_0_0>(cancellationToken);

        var stats = new List<(string Operation, TimeSpan Duration, int ResultCount)>();
        var sw = new Stopwatch();

        // 1. GetAllAsync - first call (cold cache)
        sw.Restart();
        List<UnitOfMeasure_1_0_0> allItems = await cacheProvider.GetAllAsync<UnitOfMeasure_1_0_0>(cancellationToken);
        sw.Stop();
        stats.Add(("GetAllAsync (cold)", sw.Elapsed, allItems.Count));

        // 2. GetAllAsync - subsequent call (warm cache)
        sw.Restart();
        List<UnitOfMeasure_1_0_0> allItemsCached = await cacheProvider.GetAllAsync<UnitOfMeasure_1_0_0>(cancellationToken);
        sw.Stop();
        stats.Add(("GetAllAsync (warm)", sw.Elapsed, allItemsCached.Count));

        // 3. GetByQueryAsync with predicate
        sw.Restart();
        List<UnitOfMeasure_1_0_0> baseUnits = await cacheProvider.GetByQueryAsync<UnitOfMeasure_1_0_0>(x => x.Data.IsBaseUnit == true, cancellationToken);
        sw.Stop();
        stats.Add(("GetByQueryAsync (predicate)", sw.Elapsed, baseUnits.Count));

        // 4. GetByQueryAsync with raw query string
        sw.Restart();
        List<UnitOfMeasure_1_0_0> queryResults = await cacheProvider.GetByQueryAsync<UnitOfMeasure_1_0_0>("data.IsBaseUnit:true", cancellationToken);
        sw.Stop();
        stats.Add(("GetByQueryAsync (raw query)", sw.Elapsed, queryResults.Count));

        // 5. Repeated calls to measure average latency
        var averageTimes = new List<(string Operation, TimeSpan Average)>();
        for (int i = 0; i < Iterations; i++)
        {
            sw.Restart();
            await cacheProvider.GetAllAsync<UnitOfMeasure_1_0_0>(cancellationToken);
            sw.Stop();
            averageTimes.Add(($"GetAllAsync iteration {i + 1}", sw.Elapsed));
        }

        // Build result summary
        var result = new
        {
            Summary = stats.Select(s => new
            {
                s.Operation,
                DurationMs = s.Duration.TotalMilliseconds,
                s.ResultCount
            }),
            RepeatedCallsMs = averageTimes.Select(a => new
            {
                a.Operation,
                DurationMs = a.Average.TotalMilliseconds
            }),
            AverageWarmCacheMs = averageTimes.Count > 0
                ? averageTimes.Average(a => a.Average.TotalMilliseconds)
                : 0
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
