using System.Text;
using Microsoft.OpenApi;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Generates C# properties and validation attributes from OpenAPI property schemas.
/// </summary>
public class PropertyGenerator
{
    private readonly TypeNameResolver _typeNameResolver;

    public PropertyGenerator(TypeNameResolver typeNameResolver)
    {
        _typeNameResolver = typeNameResolver;
    }

    public void GenerateProperty(
        StringBuilder sb,
        string propName,
        IOpenApiSchema propSchema,
        bool isRequired,
        string prefix,
        string parentName,
        string? csharpNameOverride = null)
    {
        var csharpName = csharpNameOverride ?? SchemaHelpers.Sanitize(propName.ToPascalCase());
        if (csharpName == "Unknown" && !propName.Any(char.IsLetterOrDigit))
            return;

        if (csharpName == SchemaHelpers.Sanitize(parentName))
            csharpName += "Value";

        if (propSchema.Description is not null)
            SchemaHelpers.AppendSummary(sb, propSchema.Description, prefix);

        GenerateValidationAttributes(sb, propSchema, isRequired, prefix);

        sb.AppendLine($"{prefix}[JsonPropertyName(\"{propName}\")]");

        var resolvedTypeName = _typeNameResolver.ResolveTypeName(propSchema, parentName, propName);

        // Add FlexibleBooleanConverter for boolean properties to handle non-standard JSON boolean values
        if (resolvedTypeName == "bool")
            sb.AppendLine($"{prefix}[JsonConverter(typeof(BooleanConverter))]");

        // Add NullableDateTimeOffsetConverter for DateTimeOffset properties to handle empty/invalid date strings
        if (resolvedTypeName == "DateTimeOffset")
            sb.AppendLine($"{prefix}[JsonConverter(typeof(NullableDateTimeOffsetConverter))]");

        // Non-required properties are nullable; required properties are not.
        bool isNullable = !isRequired || SchemaHelpers.IsExplicitlyNullable(propSchema);
        var nullable = isNullable ? "?" : "";

        // Use C# 'required' keyword for required properties to enforce compile-time initialization
        var requiredModifier = isRequired ? "required " : "";

        sb.AppendLine($"{prefix}public {requiredModifier}{resolvedTypeName}{nullable} {csharpName} {{ get; set; }}");
        sb.AppendLine();
    }

    private static void GenerateValidationAttributes(StringBuilder sb, IOpenApiSchema schema, bool isRequired, string prefix)
    {
        if (isRequired)
            sb.AppendLine($"{prefix}[Required]");

        if (schema.MinLength.HasValue)
            sb.AppendLine($"{prefix}[MinLength({schema.MinLength.Value})]");

        if (schema.MaxLength.HasValue)
            sb.AppendLine($"{prefix}[MaxLength({schema.MaxLength.Value})]");

        if (schema.Minimum is not null)
        {
            var max = schema.Maximum is not null ? schema.Maximum : "double.MaxValue";
            sb.AppendLine($"{prefix}[Range({schema.Minimum}, {max})]");
        }

        if (schema.MinItems.HasValue)
            sb.AppendLine($"{prefix}[MinLength({schema.MinItems.Value})]");

        if (schema.MaxItems.HasValue)
            sb.AppendLine($"{prefix}[MaxLength({schema.MaxItems.Value})]");

        if (!string.IsNullOrEmpty(schema.Pattern))
        {
            // Escape double quotes for C# verbatim string literals (@"...")
            // where " must be doubled to ""
            var escapedPattern = schema.Pattern.Replace("\"", "\"\"");
            sb.AppendLine($"{prefix}[RegularExpression(@\"{escapedPattern}\")]");
        }
    }
}