using Osdu.Client.Data.MasterData;
using Osdu.Client.Data.ReferenceData;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions.Validations;
using System.Text;

namespace Osdu.Client.ExampleApp.Examples.Validations;

public class ValidateMultipleRecordsExample(IOsduDataValidator validator) : ExampleBase
{
    public override string Category => ExampleCategory.Validations;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => "Validates multiple record types (Wellbore, Sample) showing that the same validator works for any OSDU type.";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // --- 1. Validate a Wellbore record ---
        sb.AppendLine("=== 1. Wellbore Validation ===");

        var wellboreData = new Wellbore_1_3_0_Data
        {
            ExistenceKind = "osdu:reference-data--ExistenceKind:Active:",
            TechnicalAssuranceID = "osdu:reference-data--TechnicalAssuranceType:Definitive:",
        };

        var wellboreResult = await validator.For(wellboreData)
            .Validate<ExistenceKind_1_0_0>(r => r.ExistenceKind, x => x.Id)
            .Validate<TechnicalAssuranceType_1_0_0>(r => r.TechnicalAssuranceID, x => x.Id)
            .ExecuteAsync(cancellationToken);

        sb.AppendLine($"  Valid: {wellboreResult.IsValid} ({wellboreResult.Errors.Count} errors)");
        foreach (var error in wellboreResult.Errors)
            sb.AppendLine($"    ✗ {error}");

        // --- 2. Validate a Sample record ---
        sb.AppendLine();
        sb.AppendLine("=== 2. Sample Validation ===");

        var sampleData = new Sample_3_1_0_Data
        {
            ExistenceKind = "osdu:reference-data--ExistenceKind:BogusKind:",
        };

        var sampleResult = await validator.For(sampleData)
            .Validate<ExistenceKind_1_0_0>(r => r.ExistenceKind, x => x.Id)
            .ExecuteAsync(cancellationToken);

        sb.AppendLine($"  Valid: {sampleResult.IsValid} ({sampleResult.Errors.Count} errors)");
        foreach (var error in sampleResult.Errors)
            sb.AppendLine($"    ✗ {error}");

        // --- 3. Combine results from multiple records ---
        sb.AppendLine();
        sb.AppendLine("=== 3. Combined Result ===");

        var combined = OsduValidationResult.Combine([wellboreResult, sampleResult]);
        sb.AppendLine($"  All valid: {combined.IsValid} ({combined.Errors.Count} total errors)");

        return sb.ToString();
    }
}