using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Generates C# classes, enums, and discriminated unions from OpenAPI schemas.
/// </summary>
public class TypeGenerator
{
    private readonly SchemaGeneratorContext _context;
    private readonly SchemaResolver _resolver;
    private readonly TypeNameResolver _typeNameResolver;
    private readonly PropertyGenerator _propertyGenerator;

    public TypeGenerator(SchemaGeneratorContext context, SchemaResolver resolver, TypeNameResolver typeNameResolver, PropertyGenerator propertyGenerator)
    {
        _context = context;
        _resolver = resolver;
        _typeNameResolver = typeNameResolver;
        _propertyGenerator = propertyGenerator;
    }

    public void GenerateType(StringBuilder sb, string name, IOpenApiSchema schema, int indent)
    {
        var prefix = new string(' ', indent * 4);

        if (schema.Enum is { Count: > 0 })
        {
            GenerateEnum(sb, name, schema, prefix);
            return;
        }

        if (schema.OneOf is { Count: > 0 } && _resolver.HasMeaningfulVariants(schema.OneOf))
        {
            GenerateDiscriminatedUnion(sb, name, schema.OneOf, schema.Discriminator, prefix);
            return;
        }

        if (schema.AnyOf is { Count: > 0 } && _resolver.HasMeaningfulVariants(schema.AnyOf))
        {
            GenerateDiscriminatedUnion(sb, name, schema.AnyOf, schema.Discriminator, prefix);
            return;
        }

        var (baseClass, allProperties) = _resolver.ResolveAllOf(schema);

        if (schema.Description is not null)
            SchemaHelpers.AppendSummary(sb, schema.Description, prefix);

        var derivedTypes = _resolver.FindDerivedSchemas(name, schema);
        if (derivedTypes.Count > 0 && schema.Discriminator is not null)
        {
            sb.AppendLine($"{prefix}[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{schema.Discriminator.PropertyName ?? "type"}\")]");
            foreach (var (derivedName, _) in derivedTypes)
            {
                sb.AppendLine($"{prefix}[JsonDerivedType(typeof({SchemaHelpers.Sanitize(derivedName)}), \"{derivedName}\")]");
            }
        }

        var inheritance = baseClass is not null ? $" : {baseClass}" : "";
        sb.AppendLine($"{prefix}public class {SchemaHelpers.Sanitize(name)}{inheritance}");
        sb.AppendLine($"{prefix}{{");

        var properties = allProperties.Count > 0 ? allProperties : schema.Properties;
        var required = schema.Required ?? new HashSet<string>();

        if (properties is not null)
        {
            var pascalCaseGroups = properties.Keys
                .GroupBy(p => SchemaHelpers.Sanitize(p.ToPascalCase()), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var (propName, propSchema) in properties)
            {
                string? csharpNameOverride = pascalCaseGroups.Contains(propName) ? propName : null;
                _propertyGenerator.GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", name, csharpNameOverride);
            }
        }

        sb.AppendLine($"{prefix}}}");

        if (properties is not null)
        {
            foreach (var (propName, propSchema) in properties)
            {
                GenerateInlineEnums(sb, propName, propSchema, prefix, name);
                GenerateInlineObjects(sb, propName, propSchema, prefix, name);
            }
        }
    }

    public void GenerateEnum(StringBuilder sb, string name, IOpenApiSchema schema, string prefix)
    {
        if (schema.Description is not null)
            SchemaHelpers.AppendSummary(sb, schema.Description, prefix);

        sb.AppendLine($"{prefix}[JsonConverter(typeof(JsonStringEnumConverter))]");
        sb.AppendLine($"{prefix}public enum {SchemaHelpers.Sanitize(name)}");
        sb.AppendLine($"{prefix}{{");

        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in schema.Enum?.OfType<JsonNode>() ?? [])
        {
            var stringValue = value.ToString();
            var memberName = SchemaHelpers.Sanitize(stringValue.ToPascalCase());

            if (!usedNames.Add(memberName))
            {
                memberName = SchemaHelpers.Sanitize(stringValue.ToPascalCase() + "_" + (char.IsLower(stringValue[0]) ? "Lower" : "Upper"));
                usedNames.Add(memberName);
            }

            sb.AppendLine($"{prefix}    [JsonStringEnumMemberName(\"{stringValue}\")]");
            sb.AppendLine($"{prefix}    {memberName},");
            sb.AppendLine();
        }

        sb.AppendLine($"{prefix}}}");
    }

    public void GenerateDiscriminatedUnion(
        StringBuilder sb,
        string name,
        IList<IOpenApiSchema> variants,
        OpenApiDiscriminator? discriminator,
        string prefix)
    {
        var resolvedVariants = new List<(string TypeName, string DiscriminatorValue, IOpenApiSchema Schema)>();
        int inlineIndex = 0;

        foreach (var variant in variants)
        {
            var refName = SchemaHelpers.GetSchemaReferenceName(variant);
            if (refName is not null)
            {
                var resolved = _resolver.ResolveSchemaFully((OpenApiSchemaReference)variant);
                if (resolved is null || !_resolver.SchemaHasSubstance(resolved))
                    continue;

                resolvedVariants.Add((SchemaHelpers.Sanitize(refName.ToPascalCase()), refName, variant));
            }
            else
            {
                if (!_resolver.SchemaHasSubstance(variant))
                    continue;

                var title = variant.Title;
                string typeName;
                string discriminatorValue;

                if (!string.IsNullOrEmpty(title))
                {
                    typeName = $"{SchemaHelpers.Sanitize(name)}{SchemaHelpers.Sanitize(title.ToPascalCase())}";
                    discriminatorValue = title;
                }
                else
                {
                    typeName = $"{SchemaHelpers.Sanitize(name)}Variant{++inlineIndex}";
                    discriminatorValue = typeName;
                }

                resolvedVariants.Add((typeName, discriminatorValue, variant));
            }
        }

        if (resolvedVariants.Count < 2)
            return;

        sb.AppendLine($"{prefix}[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{discriminator?.PropertyName ?? "type"}\")]");

        foreach (var (typeName, discriminatorValue, _) in resolvedVariants)
        {
            sb.AppendLine($"{prefix}[JsonDerivedType(typeof({typeName}), \"{discriminatorValue}\")]");
        }

        sb.AppendLine($"{prefix}public abstract class {SchemaHelpers.Sanitize(name)}");
        sb.AppendLine($"{prefix}{{");
        sb.AppendLine($"{prefix}}}");

        foreach (var (typeName, _, schema) in resolvedVariants)
        {
            var refName = SchemaHelpers.GetSchemaReferenceName(schema);
            if (refName is not null && _context.Document.Components?.Schemas?.ContainsKey(refName) == true)
            {
                if (_context.GeneratedTypes.TryGetValue(refName, out var existingCode))
                {
                    _context.GeneratedTypes[refName] = SchemaHelpers.PatchClassInheritance(existingCode, typeName, name);
                }
                else
                {
                    _context.PendingBaseClassPatches[refName] = name;
                }
                continue;
            }

            if (_context.GeneratedTypes.ContainsKey(typeName))
                continue;

            sb.AppendLine();

            var resolvedSchema = schema is OpenApiSchemaReference schemaRef
                ? _resolver.ResolveReference(schemaRef)
                : schema;

            if (resolvedSchema is null)
                continue;

            if (resolvedSchema.Description is not null)
                SchemaHelpers.AppendSummary(sb, resolvedSchema.Description, prefix);

            sb.AppendLine($"{prefix}public class {typeName} : {SchemaHelpers.Sanitize(name)}");
            sb.AppendLine($"{prefix}{{");

            if (resolvedSchema.Properties is not null)
            {
                var required = resolvedSchema.Required ?? new HashSet<string>();
                foreach (var (propName, propSchema) in resolvedSchema.Properties)
                {
                    _propertyGenerator.GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", typeName);
                }
            }

            sb.AppendLine($"{prefix}}}");

            if (resolvedSchema.Properties is not null)
            {
                foreach (var (propName, propSchema) in resolvedSchema.Properties)
                {
                    GenerateInlineEnums(sb, propName, propSchema, prefix, typeName);
                    GenerateInlineObjects(sb, propName, propSchema, prefix, typeName);
                }
            }
        }
    }

    private void GenerateInlineEnums(StringBuilder sb, string propName, IOpenApiSchema propSchema, string prefix, string parentName)
    {
        if (propSchema is OpenApiSchemaReference)
            return;

        if (propSchema.Enum is { Count: > 0 } && SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
        {
            var enumName = $"{parentName}_{propName.ToPascalCase()}";
            sb.AppendLine();
            GenerateEnum(sb, enumName, propSchema, prefix);
            return;
        }

        if (SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
        {
            var itemSchema = propSchema.Items;
            if (itemSchema is not OpenApiSchemaReference && itemSchema.Enum is { Count: > 0 } && SchemaHelpers.HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
            {
                var enumName = $"{parentName}_{propName.ToPascalCase()}";
                sb.AppendLine();
                GenerateEnum(sb, enumName, itemSchema, prefix);
            }
        }
    }

    private void GenerateInlineObjects(StringBuilder sb, string propName, IOpenApiSchema propSchema, string prefix, string parentName)
    {
        if (propSchema.AllOf is { Count: > 0 })
        {
            var refs = propSchema.AllOf.OfType<OpenApiSchemaReference>().ToList();
            var inlineSchemas = propSchema.AllOf.Where(s => s is not OpenApiSchemaReference).ToList();

            if (refs.Count > 1 || (refs.Count == 1 && inlineSchemas.Any(s => s.Properties is { Count: > 0 })))
            {
                var inlineTypeName = $"{parentName}_{propName.ToPascalCase()}";
                sb.AppendLine();

                var baseClass = SchemaHelpers.Sanitize(refs[0].Reference.Id);
                var mergedProperties = new Dictionary<string, IOpenApiSchema>();

                // Resolve additional $ref schemas and merge their properties
                foreach (var refSchema in refs.Skip(1))
                {
                    var resolved = _resolver.ResolveSchemaFully(refSchema);
                    if (resolved?.Properties is not null)
                    {
                        foreach (var (key, value) in resolved.Properties)
                            mergedProperties.TryAdd(key, value);
                    }
                }

                foreach (var inlineSchema in inlineSchemas)
                {
                    if (inlineSchema.Properties is not null)
                    {
                        foreach (var (key, value) in inlineSchema.Properties)
                            mergedProperties.TryAdd(key, value);
                    }
                }

                if (propSchema.Description is not null)
                    SchemaHelpers.AppendSummary(sb, propSchema.Description, prefix);

                var additionalBases = refs.Skip(1).Select(r => SchemaHelpers.Sanitize(r.Reference.Id)).ToList();
                var commentSuffix = additionalBases.Count > 0
                    ? $" // Also composes: {string.Join(", ", additionalBases)}"
                    : "";

                sb.AppendLine($"{prefix}public class {SchemaHelpers.Sanitize(inlineTypeName)} : {baseClass}{commentSuffix}");
                sb.AppendLine($"{prefix}{{");

                foreach (var (key, value) in mergedProperties)
                {
                    _propertyGenerator.GenerateProperty(sb, key, value, false, prefix + "    ", inlineTypeName);
                }

                sb.AppendLine($"{prefix}}}");

                foreach (var (key, value) in mergedProperties)
                {
                    GenerateInlineEnums(sb, key, value, prefix, inlineTypeName);
                    GenerateInlineObjects(sb, key, value, prefix, inlineTypeName);
                }

                return;
            }
        }

        if (propSchema.OneOf is { Count: > 0 } || propSchema.AnyOf is { Count: > 0 })
        {
            var variants = propSchema.OneOf is { Count: > 0 } ? propSchema.OneOf : propSchema.AnyOf!;

            if (!_resolver.HasMeaningfulVariants(variants))
                return;

            var commonBase = _resolver.FindCommonBaseClass(variants);
            if (commonBase is not null)
                return;

            var signature = SchemaHelpers.GetOneOfSignature(variants);
            if (signature is not null && _context.OneOfUnionCache.ContainsKey(signature))
            {
                var expectedName = SchemaHelpers.Sanitize($"{parentName}_{propName.ToPascalCase()}");
                if (_context.OneOfUnionCache[signature] != expectedName)
                    return;
            }

            var inlineTypeName = (signature is not null ? _context.OneOfUnionCache.GetValueOrDefault(signature) : null)
                                 ?? SchemaHelpers.Sanitize($"{parentName}_{propName.ToPascalCase()}");
            sb.AppendLine();
            GenerateDiscriminatedUnion(sb, inlineTypeName, variants, propSchema.Discriminator, prefix);
            return;
        }

        if (propSchema is not OpenApiSchemaReference
            && SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
            && propSchema.Properties is { Count: > 0 }
            && propSchema.AdditionalProperties is null)
        {
            var inlineTypeName = $"{parentName}_{propName.ToPascalCase()}";
            sb.AppendLine();
            GenerateType(sb, inlineTypeName, propSchema, 0);
            return;
        }

        if (SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
        {
            var itemSchema = propSchema.Items;

            if (itemSchema is not OpenApiSchemaReference && itemSchema.AllOf is { Count: > 0 })
            {
                var inlineTypeName = $"{parentName}_{propName.ToPascalCase()}";
                sb.AppendLine();
                GenerateType(sb, inlineTypeName, itemSchema, 0);
                return;
            }

            if (itemSchema is not OpenApiSchemaReference
                && SchemaHelpers.HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
                && itemSchema.Properties is { Count: > 0 }
                && itemSchema.AdditionalProperties is null)
            {
                var inlineTypeName = $"{parentName}_{propName.ToPascalCase()}";
                sb.AppendLine();
                GenerateType(sb, inlineTypeName, itemSchema, 0);
                return;
            }

            if (itemSchema is not OpenApiSchemaReference
                && (itemSchema.OneOf is { Count: > 0 } || itemSchema.AnyOf is { Count: > 0 }))
            {
                var variants = itemSchema.OneOf is { Count: > 0 } ? itemSchema.OneOf : itemSchema.AnyOf!;

                if (!_resolver.HasMeaningfulVariants(variants))
                    return;

                var commonBase = _resolver.FindCommonBaseClass(variants);
                if (commonBase is not null)
                    return;

                var signature = SchemaHelpers.GetOneOfSignature(variants);
                if (signature is not null && _context.OneOfUnionCache.ContainsKey(signature))
                {
                    var expectedName = SchemaHelpers.Sanitize($"{parentName}_{propName.ToPascalCase()}");
                    if (_context.OneOfUnionCache[signature] != expectedName)
                        return;
                }

                var inlineTypeName = (signature is not null ? _context.OneOfUnionCache.GetValueOrDefault(signature) : null)
                                     ?? SchemaHelpers.Sanitize($"{parentName}_{propName.ToPascalCase()}");
                sb.AppendLine();
                GenerateDiscriminatedUnion(sb, inlineTypeName, variants, itemSchema.Discriminator, prefix);
                return;
            }
        }
    }
}
