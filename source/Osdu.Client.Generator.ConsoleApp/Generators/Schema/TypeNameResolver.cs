using Microsoft.OpenApi;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Resolves OpenAPI schemas to C# type names.
/// </summary>
public class TypeNameResolver
{
    private readonly SchemaResolver _resolver;
    private readonly SchemaGeneratorContext _context;

    public TypeNameResolver(SchemaResolver resolver, SchemaGeneratorContext context)
    {
        _resolver = resolver;
        _context = context;
    }

    /// <summary>
    /// Builds a short inline type name anchored to the root schema name.
    /// </summary>
    private string BuildInlineName(string parentName, string propertyName)
    {
        var pascal = propertyName.ToPascalCase();
        var shortName = _context.GetShortInlineName(parentName, pascal);
        return SchemaHelpers.Sanitize(shortName);
    }

    public string ResolveTypeName(IOpenApiSchema schema, string parentName, string propertyName)
    {
        if (schema is OpenApiSchemaReference schemaRef)
            return SchemaHelpers.Sanitize(schemaRef.Reference.Id);

        if (schema.OneOf is { Count: > 0 })
        {
            if (!_resolver.HasMeaningfulVariants(schema.OneOf))
                return _resolver.ResolveNonMeaningfulOneOfType(schema.OneOf);

            var substantiveVariants = schema.OneOf
                .Where(v => _resolver.SchemaHasSubstance(v is OpenApiSchemaReference sr ? _resolver.ResolveSchemaFully(sr) ?? v : v))
                .ToList();

            if (substantiveVariants.Count == 1)
                return ResolveTypeName(substantiveVariants[0], parentName, propertyName);

            var commonBase = _resolver.FindCommonBaseClass(schema.OneOf);
            if (commonBase is not null)
                return SchemaHelpers.Sanitize(commonBase);

            var signature = SchemaHelpers.GetOneOfSignature(schema.OneOf);
            if (signature is not null && _context.OneOfUnionCache.TryGetValue(signature, out var cachedType))
                return cachedType;

            var unionName = BuildInlineName(parentName, propertyName);
            if (signature is not null)
                _context.OneOfUnionCache[signature] = unionName;

            return unionName;
        }

        if (schema.AnyOf is { Count: > 0 })
        {
            if (!_resolver.HasMeaningfulVariants(schema.AnyOf))
                return _resolver.ResolveNonMeaningfulOneOfType(schema.AnyOf);

            var substantiveVariants = schema.AnyOf
                .Where(v => _resolver.SchemaHasSubstance(v is OpenApiSchemaReference sr ? _resolver.ResolveSchemaFully(sr) ?? v : v))
                .ToList();

            if (substantiveVariants.Count == 1)
                return ResolveTypeName(substantiveVariants[0], parentName, propertyName);

            var commonBase = _resolver.FindCommonBaseClass(schema.AnyOf);
            if (commonBase is not null)
                return SchemaHelpers.Sanitize(commonBase);

            var signature = SchemaHelpers.GetOneOfSignature(schema.AnyOf);
            if (signature is not null && _context.OneOfUnionCache.TryGetValue(signature, out var cachedType))
                return cachedType;

            var unionName = BuildInlineName(parentName, propertyName);
            if (signature is not null)
                _context.OneOfUnionCache[signature] = unionName;

            return unionName;
        }

        if (schema.AllOf is { Count: > 0 })
        {
            var refs = schema.AllOf.OfType<OpenApiSchemaReference>().ToList();
            if (refs.Count > 1)
                return BuildInlineName(parentName, propertyName);

            if (refs.Count == 1)
            {
                var inlineProps = schema.AllOf
                    .Where(s => s is not OpenApiSchemaReference && s.Properties is { Count: > 0 })
                    .SelectMany(s => s.Properties)
                    .ToList();

                if (inlineProps.Count > 0)
                    return BuildInlineName(parentName, propertyName);

                return SchemaHelpers.Sanitize(refs[0].Reference.Id);
            }
        }

        var type = schema.Type ?? JsonSchemaType.Null;
        var format = schema.Format;

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.String))
        {
            if (schema.Enum is { Count: > 0 })
                return BuildInlineName(parentName, propertyName);
            return format switch
            {
                "date-time" => "DateTimeOffset",
                "date" => "DateOnly",
                "time" => "TimeOnly",
                "uuid" => "Guid",
                "uri" => "Uri",
                "binary" => "byte[]",
                _ => "string"
            };
        }

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.Integer))
            return format == "int64" ? "long" : "int";

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.Number))
            return format switch
            {
                "float" => "float",
                "decimal" => "decimal",
                _ => "double"
            };

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.Boolean))
            return "bool";

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.Array))
        {
            var itemSchema = schema.Items;
            if (itemSchema is not null)
            {
                var itemOneOf = itemSchema.OneOf is { Count: > 0 } ? itemSchema.OneOf
                    : itemSchema.AnyOf is { Count: > 0 } ? itemSchema.AnyOf
                    : null;

                if (itemOneOf is not null)
                {
                    if (!_resolver.HasMeaningfulVariants(itemOneOf))
                        return $"List<{_resolver.ResolveNonMeaningfulOneOfType(itemOneOf)}>";

                    var commonBase = _resolver.FindCommonBaseClass(itemOneOf);
                    if (commonBase is not null)
                        return $"List<{SchemaHelpers.Sanitize(commonBase)}>";

                    var signature = SchemaHelpers.GetOneOfSignature(itemOneOf);
                    if (signature is not null && _context.OneOfUnionCache.TryGetValue(signature, out var cachedType))
                        return $"List<{cachedType}>";

                    var unionName = BuildInlineName(parentName, propertyName);
                    if (signature is not null)
                        _context.OneOfUnionCache[signature] = unionName;

                    return $"List<{unionName}>";
                }

                var itemType = ResolveTypeName(itemSchema, parentName, propertyName);
                return $"List<{itemType}>";
            }
            return "List<object>";
        }

        if (SchemaHelpers.HasFlag(type, JsonSchemaType.Object))
        {
            if (schema.AdditionalProperties is not null)
                return $"Dictionary<string, {ResolveTypeName(schema.AdditionalProperties, parentName, propertyName)}>";
            if (schema.Properties?.Count > 0)
                return BuildInlineName(parentName, propertyName);
        }

        return "object";
    }
}