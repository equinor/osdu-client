using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.WellboreDdms;

public class GetVersionExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.WellboreDdms;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.WellboreDdms.GetVersionAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
