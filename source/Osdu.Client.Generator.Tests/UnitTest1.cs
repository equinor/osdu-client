using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Osdu.Client.Apis.CrsConversion;
using Osdu.Client.Apis.Search;
using Osdu.Client.Extensions;
using Osdu.Client.Schemas.MasterData;

//using Osdu.Client.Schemas.MasterData;

namespace Osdu.Client.Generator.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        ISearchApiClient searchClient = new SearchApiClient(null);
        //await searchClient.QueryRecordsAsync("dataPartitionId", new QueryRequest());

        ICrsConversionApiClient crsConversionClient = new CrsConversionApiClient(null);

        //Class1 class1 = new Class1();

        QueryRequest body = new QueryRequest
        {
            Kind = "osdu:wks:master-data--Wellbore:1.3.0",
            Limit = 1,
        };

        QueryResponse response = await searchClient.PostQueryAsync(body, CancellationToken.None);
        IEnumerable<Wellbore_1_5_0> list = response.Results.Deserialize<Wellbore_1_5_0>();

        //List<Wellbore_1_5_0> typedResults = response.GetResults<Wellbore_1_5_0>();

        //foreach (Wellbore1_3 result in typedResults)
        //{

        //}

        IServiceCollection services = new ServiceCollection();
        services.AddOsduApiClients();
    }
}
