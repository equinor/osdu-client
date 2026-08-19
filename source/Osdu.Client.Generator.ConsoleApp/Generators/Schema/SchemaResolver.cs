using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Resolves schema references, allOf composition, common base classes, and type names.
/// </summary>
public class SchemaResolver
{
    private readonly SchemaGeneratorContext _context;
    private readonly Dictionary<string, IOpenApiSchema> _externalSchemaCache = new(StringComparer.OrdinalIgnoreCase);

    public SchemaResolver(SchemaGeneratorContext context)
    {
        _context = context;
    }

    public IOpenApiSchema? ResolveSchemaFully(OpenApiSchemaReference schemaRef)
    {
        var id = schemaRef.Reference.Id;

        if (_context.Document.Components?.Schemas is not null &&
            _context.Document.Components.Schemas.TryGetValue(id, out var resolved))
        {
            if (resolved is OpenApiSchemaReference innerRef)
                return ResolveSchemaFully(innerRef);
            return resolved;
        }

        // Handle external $ref by loading the referenced JSON file
        var externalResource = schemaRef.Reference?.ExternalResource;
        if (!string.IsNullOrEmpty(externalResource))
        {
            return ResolveExternalSchema(externalResource);
        }

        return schemaRef;
    }

    /// <summary>
    /// Resolves an external $ref by loading and parsing the referenced JSON schema file.
    /// Results are cached to avoid repeated file I/O and parsing.
    /// </summary>
    private IOpenApiSchema? ResolveExternalSchema(string externalResource)
    {
        if (_externalSchemaCache.TryGetValue(externalResource, out var cached))
            return cached;

        try
        {
            string currentDir = Path.GetDirectoryName(_context.JsonFilePath) ?? string.Empty;
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, externalResource));

            if (!File.Exists(fullPath))
                return null;

            string jsonContent = File.ReadAllText(fullPath);
            string schemaName = Path.GetFileNameWithoutExtension(fullPath).Replace('.', '_');

            // Wrap in a minimal OpenAPI document to reuse the parser
            var wrappedJson = $$"""
                {
                    "openapi": "3.0.0",
                    "info": { "title": "{{schemaName}}", "version": "1.0.0" },
                    "paths": {},
                    "components": {
                        "schemas": {
                            "{{schemaName}}": {{jsonContent}}
                        }
                    }
                }
                """;

            var result = OpenApiDocument.Parse(wrappedJson, "json");
            var schema = result?.Document?.Components?.Schemas?.Values.FirstOrDefault();

            if (schema is not null)
            {
                _externalSchemaCache[externalResource] = schema;
            }

            return schema;
        }
        catch
        {
            return null;
        }
    }

    public IOpenApiSchema? ResolveReference(OpenApiSchemaReference schemaRef)
    {
        var id = schemaRef.Reference.Id;
        if (_context.Document.Components?.Schemas is not null &&
            _context.Document.Components.Schemas.TryGetValue(id, out var resolved))
        {
            return resolved;
        }

        if (schemaRef.Properties is { Count: > 0 })
            return schemaRef;

        return schemaRef;
    }

    public (string? BaseClass, IDictionary<string, IOpenApiSchema> Properties) ResolveAllOf(IOpenApiSchema schema)
    {
        if (schema.AllOf is not { Count: > 0 })
            return (null, schema.Properties ?? new Dictionary<string, IOpenApiSchema>());

        string? baseClass = null;
        var mergedProperties = new Dictionary<string, IOpenApiSchema>();
        var additionalRefs = new List<OpenApiSchemaReference>();

        foreach (var allOfItem in schema.AllOf)
        {
            if (allOfItem is OpenApiSchemaReference schemaRef)
            {
                if (baseClass is null)
                {
                    baseClass = SchemaHelpers.Sanitize(schemaRef.Reference.Id);
                }
                else
                {
                    additionalRefs.Add(schemaRef);
                }
            }
            else if (allOfItem.Properties is not null)
            {
                foreach (var (key, value) in allOfItem.Properties)
                {
                    mergedProperties.TryAdd(key, value);
                }
            }
        }

        // Resolve properties from additional $ref schemas that can't be inherited
        foreach (var additionalRef in additionalRefs)
        {
            var resolved = ResolveSchemaFully(additionalRef);
            if (resolved?.Properties is not null)
            {
                foreach (var (key, value) in resolved.Properties)
                {
                    mergedProperties.TryAdd(key, value);
                }
            }
        }

        if (schema.Properties is not null)
        {
            foreach (var (key, value) in schema.Properties)
            {
                mergedProperties.TryAdd(key, value);
            }
        }

        return (baseClass, mergedProperties);
    }

    public string? FindCommonBaseClass(IList<IOpenApiSchema> variants)
    {
        if (_context.Document.Components?.Schemas is null)
            return null;

        var refs = variants.OfType<OpenApiSchemaReference>().ToList();

        if (refs.Count != variants.Count || refs.Count == 0)
            return null;

        string? commonBase = null;

        foreach (var schemaRef in refs)
        {
            var id = schemaRef.Reference.Id;

            IOpenApiSchema? resolved = null;

            if (schemaRef.AllOf is { Count: > 0 })
            {
                resolved = schemaRef;
            }
            else if (_context.Document.Components.Schemas.TryGetValue(id, out var componentSchema))
            {
                resolved = componentSchema is OpenApiSchemaReference innerRef
                    ? ResolveReference(innerRef)
                    : componentSchema;

                if (resolved?.AllOf is not { Count: > 0 })
                    resolved = schemaRef;
            }

            if (resolved is null)
                return null;

            var (baseClass, _) = ResolveAllOf(resolved);

            if (baseClass is null)
                return null;

            if (commonBase is null)
                commonBase = baseClass;
            else if (commonBase != baseClass)
                return null;
        }

        return commonBase;
    }

    public List<(string Name, IOpenApiSchema Schema)> FindDerivedSchemas(string baseSchemaName, IOpenApiSchema baseSchema)
    {
        var derived = new List<(string Name, IOpenApiSchema Schema)>();

        if (_context.Document.Components?.Schemas is null)
            return derived;

        if (baseSchema.Discriminator is null)
            return derived;

        foreach (var (schemaName, schema) in _context.Document.Components.Schemas)
        {
            if (schemaName == baseSchemaName)
                continue;

            if (schema.AllOf is { Count: > 0 })
            {
                foreach (var allOfItem in schema.AllOf)
                {
                    if (allOfItem is OpenApiSchemaReference schemaRef && schemaRef.Reference.Id == baseSchemaName)
                    {
                        derived.Add((schemaName, schema));
                        break;
                    }
                }
            }
        }

        return derived;
    }

    public bool HasMeaningfulVariants(IList<IOpenApiSchema> variants)
    {
        if (variants.Count == 0)
            return false;

        foreach (var variant in variants)
        {
            if (variant is OpenApiSchemaReference schemaRef)
            {
                var resolved = ResolveSchemaFully(schemaRef);
                if (resolved is not null && SchemaHasSubstance(resolved))
                    return true;
            }
            else
            {
                if (SchemaHasSubstance(variant))
                    return true;
            }
        }

        return false;
    }

    public bool SchemaHasSubstance(IOpenApiSchema schema)
    {
        if (schema.Properties is { Count: > 0 })
            return true;

        if (schema.AllOf is { Count: > 0 })
        {
            foreach (var part in schema.AllOf)
            {
                if (part is OpenApiSchemaReference partRef)
                {
                    var resolved = ResolveSchemaFully(partRef);
                    if (resolved is not null && resolved.Properties is { Count: > 0 })
                        return true;
                }
                else if (part.Properties is { Count: > 0 })
                {
                    return true;
                }
            }
        }

        if (schema.OneOf is { Count: > 0 } && schema.OneOf.OfType<OpenApiSchemaReference>().Any())
            return true;
        if (schema.AnyOf is { Count: > 0 } && schema.AnyOf.OfType<OpenApiSchemaReference>().Any())
            return true;

        if (schema.Items is OpenApiSchemaReference)
            return true;
        if (schema.Items is not null && schema.Items.Properties is { Count: > 0 })
            return true;

        return false;
    }

    public string ResolveNonMeaningfulOneOfType(IList<IOpenApiSchema> variants)
    {
        string? resolvedType = null;

        foreach (var variant in variants)
        {
            var actual = variant is OpenApiSchemaReference sr ? ResolveSchemaFully(sr) : variant;
            if (actual is null)
                return "object";

            var type = actual.Type ?? JsonSchemaType.Null;
            var format = actual.Format;

            if (type == JsonSchemaType.Null)
                continue;

            string csharpType;

            if (SchemaHelpers.HasFlag(type, JsonSchemaType.String))
            {
                csharpType = format switch
                {
                    "date-time" => "DateTimeOffset",
                    "date" => "DateOnly",
                    "time" => "TimeOnly",
                    "uuid" => "Guid",
                    "uri" => "Uri",
                    _ => "string"
                };
            }
            else if (SchemaHelpers.HasFlag(type, JsonSchemaType.Integer))
                csharpType = format == "int64" ? "long" : "int";
            else if (SchemaHelpers.HasFlag(type, JsonSchemaType.Number))
                csharpType = format switch { "float" => "float", "decimal" => "decimal", _ => "double" };
            else if (SchemaHelpers.HasFlag(type, JsonSchemaType.Boolean))
                csharpType = "bool";
            else if (SchemaHelpers.HasFlag(type, JsonSchemaType.Array))
            {
                var itemSchema = actual.Items;
                if (itemSchema is not null)
                {
                    var itemType = new TypeNameResolver(this, _context).ResolveTypeName(itemSchema, "", "");
                    csharpType = $"List<{itemType}>";
                }
                else
                {
                    csharpType = "List<object>";
                }
            }
            else
                csharpType = "object";

            if (resolvedType is null)
                resolvedType = csharpType;
            else if (resolvedType != csharpType)
                return "object";
        }

        return resolvedType ?? "object";
    }
}
