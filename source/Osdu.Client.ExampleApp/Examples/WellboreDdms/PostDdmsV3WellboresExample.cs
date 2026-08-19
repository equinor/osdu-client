using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.WellboreDdms;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.WellboreDdms;

public class PostDdmsV3WellboresExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.WellboreDdms;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The wellbore kind.")]
    public string Kind { get; set; } = "osdu:wks:master-data--Wellbore:1.3.0";

    [ExampleParameter(DisplayName = "Legal Tag", Required = true, Order = 1, Description = "Legal tag name.")]
    public string LegalTag { get; set; } = "dev-equinor-private-default";

    [ExampleParameter(DisplayName = "Viewers ACL", Required = true, Order = 2, Description = "ACL viewers group.")]
    public string ViewersAcl { get; set; } = "data.office.global.viewers@dev.dataservices.energy";

    [ExampleParameter(DisplayName = "Owners ACL", Required = true, Order = 3, Description = "ACL owners group.")]
    public string OwnersAcl { get; set; } = "data.wellcoredb.owners@dev.dataservices.energy";

    [ExampleParameter(DisplayName = "Wellbore Name", Order = 4, Description = "Name of the wellbore to create.")]
    public string WellboreName { get; set; } = "MRK Example Wellbore A-1";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        //return "Not run as we do not want to add dummy data in OSDU. Just check the code as an example";

        var wellboreData = new Dictionary<string, object>
        {
            ["FacilityName"] = WellboreName,
            ["WellID"] = "dev:master-data--Well:ExampleWell001",
            ["TrajectoryTypeID"] = "dev:reference-data--WellboreTrajectoryType:Vertical"
        };

        var records = new List<Record>
        {
            new()
            {
                Kind = Kind,
                Acl = new StorageAcl
                {
                    Viewers = [ViewersAcl],
                    Owners = [OwnersAcl]
                },
                Legal = new Apis.WellboreDdms.Legal
                {
                    Legaltags = [LegalTag],
                    OtherRelevantDataCountries = ["NO", "US"]
                },
                Data = wellboreData
            }
        };

        var response = await osduClient.WellboreDdms.PostDdmsV3WellboresAsync(records, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
