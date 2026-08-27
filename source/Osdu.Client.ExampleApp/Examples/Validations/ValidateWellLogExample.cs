using Osdu.Client.Data.MasterData;
using Osdu.Client.Data.ReferenceData;
using Osdu.Client.Data.WorkProductComponent;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions.Caching;
using Osdu.Client.Extensions.Validations;
using System.Text;

namespace Osdu.Client.ExampleApp.Examples.Validations;

public class ValidateWellLogExample(IOsduDataValidator validator, IOsduCacheProvider cacheProvider) : ExampleBase
{
    public override string Category => ExampleCategory.Validations;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => "Validates WellLog reference-data and master-data fields using IOsduDataValidator.";

    [ExampleParameter(DisplayName = "WellboreID", Order = 0, Description = "A valid Wellbore ID to validate against.")]
    public string WellboreId { get; set; } = "";

    [ExampleParameter(DisplayName = "WellLogTypeID", Order = 1, Description = "A LogType reference-data ID (leave empty to test invalid).")]
    public string WellLogTypeId { get; set; } = "";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Pick a real wellbore ID from the cache if none provided
        if (string.IsNullOrWhiteSpace(WellboreId))
        {
            var wellbores = await cacheProvider.GetAllAsync<Wellbore_1_3_0>(cancellationToken);
            WellboreId = wellbores.FirstOrDefault()?.Id ?? "osdu:master-data--Wellbore:DoesNotExist:";
        }

        // Build a simulated WellLog data record with a mix of valid/invalid references
        var wellLogData = new WellLog_1_4_0_Data
        {
            WellboreID = WellboreId,
            WellLogTypeID = string.IsNullOrWhiteSpace(WellLogTypeId)
                ? "osdu:reference-data--LogType:CompletelyMadeUp:"
                : WellLogTypeId,
            ExistenceKind = "osdu:reference-data--ExistenceKind:Active:",
            WellboreFluidTypeID = "osdu:reference-data--WellboreFluidType:InvalidFluid:",
            Curves =
            [
                new WellLog_1_4_0_Data_Curves { CurveUnit = "osdu:reference-data--UnitOfMeasure:m:" },
                new WellLog_1_4_0_Data_Curves { CurveUnit = "osdu:reference-data--UnitOfMeasure:ft:" },
                new WellLog_1_4_0_Data_Curves { CurveUnit = "osdu:reference-data--UnitOfMeasure:banana:" },
            ]
        };

        sb.AppendLine("=== WellLog Validation ===");
        sb.AppendLine();
        sb.AppendLine("Record under test:");
        sb.AppendLine($"  WellboreID:          {wellLogData.WellboreID}");
        sb.AppendLine($"  WellLogTypeID:       {wellLogData.WellLogTypeID}");
        sb.AppendLine($"  ExistenceKind:       {wellLogData.ExistenceKind}");
        sb.AppendLine($"  WellboreFluidTypeID: {wellLogData.WellboreFluidTypeID}");
        sb.AppendLine($"  Curve units:         {string.Join(", ", wellLogData.Curves.Select(c => c.CurveUnit))}");
        sb.AppendLine();

        // Validate all reference fields in one pass
        var result = await validator.For(wellLogData)
            .Validate<Wellbore_1_3_0>(r => r.WellboreID, x => x.Id)
            .Validate<LogType_1_0_0>(r => r.WellLogTypeID, x => x.Id)
            .Validate<ExistenceKind_1_0_0>(r => r.ExistenceKind, x => x.Id)
            .Validate<WellboreFluidType_1_0_0>(r => r.WellboreFluidTypeID, x => x.Id)
            .ValidateAll<UnitOfMeasure_1_0_0, WellLog_1_4_0_Data_Curves>(
                r => r.Curves, c => c.CurveUnit, x => x.Id)
            .ExecuteAsync(cancellationToken);

        sb.AppendLine($"Valid: {result.IsValid}");
        sb.AppendLine($"Errors: {result.Errors.Count}");
        sb.AppendLine();

        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
                sb.AppendLine($"  ✗ {error}");

            sb.AppendLine();
            sb.AppendLine("--- ThrowIfInvalid demo ---");
            try
            {
                result.ThrowIfInvalid();
            }
            catch (OsduValidationException ex)
            {
                sb.AppendLine($"  Caught OsduValidationException: {ex.Result.Errors.Count} error(s)");
            }
        }

        return sb.ToString();
    }
}