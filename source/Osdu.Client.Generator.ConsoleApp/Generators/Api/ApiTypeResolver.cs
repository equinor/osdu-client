using Microsoft.OpenApi;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Resolves OpenAPI schemas to C# type names for API client generation.
/// </summary>
public class ApiTypeResolver
{
    public string ResolvePrimitiveTypeName(IOpenApiSchema? schema, string fallback = "string")
    {
        if (schema is null) return fallback;

        JsonSchemaType type = schema.Type ?? JsonSchemaType.Null;

        if ((type & JsonSchemaType.String) == JsonSchemaType.String)
            return "string";

        if ((type & JsonSchemaType.Integer) == JsonSchemaType.Integer)
            return schema.Format == "int64" ? "long" : "int";

        if ((type & JsonSchemaType.Number) == JsonSchemaType.Number)
            return "double";

        if ((type & JsonSchemaType.Boolean) == JsonSchemaType.Boolean)
            return "bool";

        return fallback;
    }

    public string ResolveParamType(IOpenApiSchema? schema)
    {
        if (schema is null) return "string";
        return ResolvePrimitiveTypeName(schema, "string");
    }

    public string ResolveSchemaTypeName(IOpenApiSchema? schema)
    {
        if (schema is null) return "object";

        if (schema is OpenApiSchemaReference schemaRef) return schemaRef.Reference.Id;

        // Handle composed schemas (allOf, oneOf, anyOf)
        if (schema.AllOf is { Count: > 0 })
        {
            foreach (IOpenApiSchema subSchema in schema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                    return allOfRef.Reference.Id!;
            }
            return ResolveSchemaTypeName(schema.AllOf[0]);
        }
        if (schema.OneOf is { Count: > 0 })
        {
            foreach (IOpenApiSchema subSchema in schema.OneOf)
            {
                if (subSchema is OpenApiSchemaReference oneOfRef)
                    return oneOfRef.Reference.Id!;
            }
            return ResolveSchemaTypeName(schema.OneOf[0]);
        }
        if (schema.AnyOf is { Count: > 0 })
        {
            foreach (var subSchema in schema.AnyOf)
            {
                if (subSchema is OpenApiSchemaReference anyOfRef)
                    return anyOfRef.Reference.Id!;
            }
            return ResolveSchemaTypeName(schema.AnyOf[0]);
        }

        JsonSchemaType type = schema.Type ?? JsonSchemaType.Null;

        if ((type & JsonSchemaType.Array) == JsonSchemaType.Array)
        {
            string itemType = ResolveSchemaTypeName(schema.Items);
            return $"List<{itemType}>";
        }

        return ResolvePrimitiveTypeName(schema, "object");
    }
}
