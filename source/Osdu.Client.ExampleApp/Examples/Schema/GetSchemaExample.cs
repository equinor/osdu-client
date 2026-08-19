using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.Schema;

public class GetSchemaExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Schema;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Authority", Order = 0, Description = "Filter by schema authority (e.g. 'osdu').")]
    public string Authority { get; set; } = "osdu";

    [ExampleParameter(DisplayName = "Entity Type", Order = 1, Description = "Filter by entity type (e.g. 'Wellbore').")]
    public string EntityType { get; set; } = "Wellbore";

    [ExampleParameter(DisplayName = "Limit", Order = 2, Description = "Maximum number of schemas to return.")]
    public int Limit { get; set; } = 10;

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Schema.GetSchemaAsync(
            authority: Authority,
            entityType: EntityType,
            latestVersion: true,
            limit: Limit,
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
