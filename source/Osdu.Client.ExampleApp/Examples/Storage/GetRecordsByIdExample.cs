using System.Text.Encodings.Web;
using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.Storage;

public class GetRecordsByIdExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Storage;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of WellLog record to search for.")]
    public string RecordId { get; set; } = "dev:work-product-component--WellLog:1c35012eab4d4c5d90eb38b9fb245522";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Storage.GetRecordsByIdAsync(RecordId, cancellationToken: cancellationToken);

        using var doc = JsonDocument.Parse(response);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
