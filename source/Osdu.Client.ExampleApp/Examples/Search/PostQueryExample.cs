using System.Text.Json;
using System.Windows.Documents;
using Osdu.Client.Apis.Search;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions.Caching;
using Osdu.Client.Extensions.Querying;
using Osdu.Client.Schemas.MasterData;
using Osdu.Client.Schemas.ReferenceData;

namespace Osdu.Client.ExampleApp.Examples.Search;

public class PostQueryExample(IOsduClient osduClient, IOsduCacheProvider cacheProvider, IOsduQueryExecutor queryExecutor) : ExampleBase
{
    public override string Category => ExampleCategory.Search;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of record to search for (supports wildcards).")]
    public string Kind { get; set; } = "osdu:wks:master-data--Wellbore:1.3.0";

    [ExampleParameter(DisplayName = "Query", Order = 1, Description = "Lucene query string.")]
    public string Query { get; set; } = "*";

    [ExampleParameter(DisplayName = "Limit", Order = 2, Description = "Maximum number of results to return.")]
    public int Limit { get; set; } = 20;

    [ExampleParameter(DisplayName = "Returned Fields", Order = 3, Description = "Comma-separated list of fields to return.")]
    public string[] ReturnedFields { get; set; } = []; //["id", "kind", "createTime"];

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        List<UnitOfMeasure_1_0_0> units = await cacheProvider.GetAllAsync<UnitOfMeasure_1_0_0>(cancellationToken);

        var cache = cacheProvider.For<UnitOfMeasure_1_0_0>();

        var list = await cacheProvider.GetByQueryAsync<UnitOfMeasure_1_0_0>(x=>x.Data.IsBaseUnit == true );

        var queryItems = await queryExecutor.ExecuteAsync<UnitOfMeasure_1_0_0>("osdu:wks:reference-data--UnitOfMeasure:1.0.0", x => x.Data.IsBaseUnit == true);

        //AzureStorageLocation? storageLocation = await osduClient.Dataset.GetAzureStorageLocationAsync("test","1h", cancellationToken);

        var result = await queryExecutor
            .Query<Wellbore_1_3_0>("osdu:wks:master-data--Wellbore:1.3.0")
            .Where(w => w.Data.WellID == "well-123")
            .Select(w => w.Id, w => w.Kind, w => w.Data.WellID, w => w.Data.DrillingReasons)
            .OrderBy(x=>x.Data.WellID)
            .OrderBy(x=> x.Data.FluidDirectionID)
            .ExecuteAsync(cancellationToken);

        var result1 = await queryExecutor
            .Query<Wellbore_1_2_0>("osdu:wks:master-data--Wellbore:1.2.0")
            //.Where(w => w.Data.WellID == "dev:master-data--Well:Drogon-55-33-2")
            .Where(w => w.Data.WellID.MatchesPattern("33-A"))
            //.Select(w => w.Id, w => w.Kind, w => w.Data.WellID, w => w.Data.DrillingReasons)
            .ExecuteAsync(cancellationToken);

        var request = new QueryRequest
        {
            Kind = Kind,
            Limit = Limit,
            Query = Query,
            ReturnedFields = ReturnedFields.ToList()
        };

        // /////////////////
        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
