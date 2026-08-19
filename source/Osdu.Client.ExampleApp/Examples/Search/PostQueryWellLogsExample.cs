using Osdu.Client.Apis.Search;
using Osdu.Client.Data.WorkProductComponent;
using Osdu.Client.ExampleApp.Controls;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions;
using System.Text.Json;
using System.Windows;

namespace Osdu.Client.ExampleApp.Examples;

public class PostQueryWellLogsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Search;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.Search.PostQuery' api endpoint with strongly-typed 'Wellbore_1_3_0' result";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of WellLog record to search for.")]
    public string Kind { get; set; } = "osdu:wks:work-product-component--WellLog:1.4.0";

    [ExampleParameter(DisplayName = "Limit", Order = 1, Description = "Maximum number of results to return.")]
    public int Limit { get; set; } = 10;

    [ExampleParameter(DisplayName = "Returned Fields", Order = 2, Description = "Comma-separated list of fields to return.")]
    public string[] ReturnedFields { get; set; } = []; //["id", "kind", "createTime"];

    [ExampleParameter(DisplayName = "Show Data List", Order = 3, Description = "Whether to show strongly typed data list or raw response.")]
    public bool ShowDataList { get; set; } = false;

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = Kind,
            Query = "*",
            Limit = Limit,
            ReturnedFields = ReturnedFields.ToList(),
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        if (!ShowDataList)
        {
            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }

        IEnumerable<WellLog_1_4_0> wellLogs = response.Results.Deserialize<WellLog_1_4_0>();

        // Flatten all curves with their parent WellLog ID for DataGrid display
        var items = wellLogs
            .Where(wl => wl.Data?.Curves is not null)
            .SelectMany(wl => wl.Data!.Curves!.Select(c => new
            {
                WellLogID = wl.Id,
                c.CurveID,
                c.Mnemonic,
                c.CurveUnit,
                c.CurveDescription,
                c.LogCurveMainFamilyID,
                c.TopDepth,
                c.BaseDepth,
                c.DepthUnit,
                c.CurveQuality,
                c.NumberOfColumns
            }))
            .ToList();

        // Switch response area to DataGrid on the UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            ResponseDisplayService.ShowDataGrid(items);
        });

        return $"Displayed {items.Count} curves from {wellLogs.Count()} well logs in DataGrid.";
    }
}
