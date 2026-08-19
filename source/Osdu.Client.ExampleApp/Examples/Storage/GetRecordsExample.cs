using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.Storage;

public class GetRecordsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Storage;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";


    [ExampleParameter(DisplayName = "Limit", Order = 0, Description = "The maximum number of records to retrieve.")]
    public int Limit { get; set; } = 10;

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Storage.GetRecordsAsync(Limit, cancellationToken: cancellationToken);

        using var doc = JsonDocument.Parse(response);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
