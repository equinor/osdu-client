using Osdu.Client.Apis.WellboreDdms;
using Osdu.Client.Data.WorkProductComponent;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using System.Text.Json;

namespace Osdu.Client.ExampleApp.Examples.WellboreDdms;

public class PostDdmsV3WelllogsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.WellboreDdms;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The wellbore kind.")]
    public string Kind { get; set; } = "osdu:wks:work-product-component--WellLog:1.5.0";

    [ExampleParameter(DisplayName = "Legal Tag", Required = true, Order = 1, Description = "Legal tag name.")]
    public string LegalTag { get; set; } = "dev-equinor-private-default";

    [ExampleParameter(DisplayName = "Viewers ACL", Required = true, Order = 2, Description = "ACL viewers group.")]
    public string ViewersAcl { get; set; } = "data.office.global.viewers@dev.dataservices.energy";

    [ExampleParameter(DisplayName = "Owners ACL", Required = true, Order = 3, Description = "ACL owners group.")]
    public string OwnersAcl { get; set; } = "data.wellcoredb.owners@dev.dataservices.energy";

    [ExampleParameter(DisplayName = "WellboreId", Order = 4, Description = "Wellbore Id")]
    public string WellboreId { get; set; } = "dev:master-data--Wellbore:3728af7d649d4df4805d38d38aeae659";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        //return "Not run as we do not want to add dummy data in OSDU. Just check the code as an example";

        WellLog_1_5_0_Data wellLog = new WellLog_1_5_0_Data()
        {
            WellboreID = WellboreId,
            TopMeasuredDepth = 1002.0,
            BottomMeasuredDepth = 2002.0,
            IsRegular = true,
            Curves = [new WellLog_1_5_0_Data_Curves { CurveID = "MRKCurve1", Mnemonic = "MRKCurve1", CurveDescription = "MRK Curve1", NumberOfColumns = 1 }]
        };

        List<Record> records = new List<Record>()
        {
            new Record()
            {
                Kind = Kind,
                Acl = new StorageAcl {Viewers = [ViewersAcl], Owners = [OwnersAcl]},
                Legal = new Apis.WellboreDdms.Legal {Legaltags = [LegalTag], OtherRelevantDataCountries = ["NO","US"]},
                Data = wellLog
            }
        };

        var response = await osduClient.WellboreDdms.PostDdmsV3WelllogsAsync(records);

        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return prettyJson;
    }
}
