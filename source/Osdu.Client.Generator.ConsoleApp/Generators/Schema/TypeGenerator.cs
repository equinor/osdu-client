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

    /// <summary>
    /// Builds a short inline type name anchored to the root schema name.
    /// </summary>
    private string BuildInlineName(string parentName, string propertyName)
    {
        var pascal = propertyName.ToPascalCase();
        return _context.GetShortInlineName(parentName, pascal);
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
            // FIX: Add UnknownDerivedTypeHandling to match GenerateDiscriminatedUnion behavior
            // and prevent NotSupportedException when JSON has an unrecognized discriminator value
            sb.AppendLine($"{prefix}[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{schema.Discriminator.PropertyName ?? "type"}\", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]");
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

        // Determine if this class is a derived type that inherits from a polymorphic base.
        // If so, find the discriminator property name to handle it specially.
        string? activeDiscriminatorPropName = FindDiscriminatorForDerivedType(name);

        if (properties is not null)
        {
            var pascalCaseGroups = properties.Keys
                .GroupBy(p => SchemaHelpers.Sanitize(p.ToPascalCase()), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var (propName, propSchema) in properties)
            {
                // For the discriminator property on derived types, emit a [JsonIgnore] string
                // property with a default value. System.Text.Json reserves the discriminator
                // JSON property name as metadata — any [JsonPropertyName] matching it on a
                // derived class throws InvalidOperationException at runtime. This is NOT a C#
                // inheritance issue; the 'new' keyword does not help.
                if (activeDiscriminatorPropName is not null
                    && string.Equals(propName, activeDiscriminatorPropName, StringComparison.Ordinal))
                {
                    var defaultValue = ExtractDiscriminatorEnumValue(schema, propName);
                    EmitDiscriminatorProperty(sb, propName, defaultValue, prefix + "    ");
                    continue;
                }

                string? csharpNameOverride = pascalCaseGroups.Contains(propName) ? propName : null;
                _propertyGenerator.GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", name, csharpNameOverride);
            }
        }

        sb.AppendLine($"{prefix}}}");

        if (properties is not null)
        {
            foreach (var (propName, propSchema) in properties)
            {
                // Skip inline enum/object generation for the discriminator property
                // since we emit it as a simple string, not a typed enum.
                if (activeDiscriminatorPropName is not null
                    && string.Equals(propName, activeDiscriminatorPropName, StringComparison.Ordinal))
                    continue;

                GenerateInlineEnums(sb, propName, propSchema, prefix, name);
                GenerateInlineObjects(sb, propName, propSchema, prefix, name);
            }
        }
    }

    /// <summary>
    /// If the given schema name is registered as a derived type of a polymorphic base
    /// (via FindDerivedSchemas + discriminator), returns the discriminator property name.
    /// This is used to emit the discriminator property with [JsonIgnore] and a default value
    /// instead of a regular serializable property, avoiding System.Text.Json metadata conflicts.
    /// </summary>
    private string? FindDiscriminatorForDerivedType(string schemaName)
    {
        if (_context.Document.Components?.Schemas is null)
            return null;

        foreach (var (baseName, baseSchema) in _context.Document.Components.Schemas)
        {
            if (baseName == schemaName || baseSchema.Discriminator is null)
                continue;

            if (baseSchema.AllOf is { Count: > 0 } || baseSchema.OneOf is { Count: > 0 } || baseSchema.AnyOf is { Count: > 0 })
                continue;

            // Check if this schema has derived types that include our schemaName
            var derived = _resolver.FindDerivedSchemas(baseName, baseSchema);
            if (derived.Any(d => d.Name == schemaName))
            {
                return baseSchema.Discriminator.PropertyName ?? "type";
            }
        }

        return null;
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
        var discriminatorPropName = discriminator?.PropertyName ?? "type";
        var resolvedVariants = new List<(string TypeName, string DiscriminatorValue, IOpenApiSchema Schema, bool IsExternal)>();
        int inlineIndex = 0;

        foreach (var variant in variants)
        {
            var refName = SchemaHelpers.GetSchemaReferenceName(variant);
            if (refName is not null)
            {
                var resolved = _resolver.ResolveSchemaFully((OpenApiSchemaReference)variant);
                if (resolved is null || !_resolver.SchemaHasSubstance(resolved))
                    continue;

                var schemaRef = (OpenApiSchemaReference)variant;
                bool isExternal = !string.IsNullOrEmpty(schemaRef.Reference?.ExternalResource);

                // Use Sanitize directly (not ToPascalCase) so the type name matches
                // the class name generated from the schema's own file
                resolvedVariants.Add((SchemaHelpers.Sanitize(refName), refName, variant, isExternal));
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
                    // Use root-anchored short name: {root}_{sanitizedTitle}
                    // instead of {fullUnionBaseName}{title}
                    var sanitizedTitle = SchemaHelpers.Sanitize(title.ToPascalCase());
                    typeName = SchemaHelpers.Sanitize(_context.GetShortVariantName(name, sanitizedTitle));

                    // Use the actual enum value from the discriminator property (e.g., "AnyCrsPoint")
                    // instead of the title (e.g., "AnyCrsGeoJSON Point") which is just a human-readable label.
                    discriminatorValue = ExtractDiscriminatorEnumValue(variant, discriminatorPropName) ?? title;
                }
                else
                {
                    typeName = $"{SchemaHelpers.Sanitize(name)}Variant{++inlineIndex}";
                    discriminatorValue = typeName;
                }

                resolvedVariants.Add((typeName, discriminatorValue, variant, false));
            }
        }

        if (resolvedVariants.Count < 2)
            return;

        // Use non-abstract class with FallBackToBaseType so JSON without a type discriminator
        // deserializes gracefully instead of throwing NotSupportedException
        sb.AppendLine($"{prefix}[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{discriminatorPropName}\", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]");

        foreach (var (typeName, discriminatorValue, _, _) in resolvedVariants)
        {
            sb.AppendLine($"{prefix}[JsonDerivedType(typeof({typeName}), \"{discriminatorValue}\")]");
        }

        sb.AppendLine($"{prefix}public class {SchemaHelpers.Sanitize(name)}");
        sb.AppendLine($"{prefix}{{");
        sb.AppendLine($"{prefix}}}");

        foreach (var (typeName, discriminatorValue, schema, isExternal) in resolvedVariants)
        {
            var refName = SchemaHelpers.GetSchemaReferenceName(schema);
            if (refName is not null && _context.Document.Components?.Schemas?.ContainsKey(refName) == true)
            {
                // Internal $ref — patch inheritance in the already-generated (or pending) code
                if (_context.GeneratedTypes.TryGetValue(refName, out var existingCode))
                {
                    _context.GeneratedTypes[refName] = SchemaHelpers.PatchClassInheritance(existingCode, typeName, SchemaHelpers.Sanitize(name));
                }
                else
                {
                    _context.PendingBaseClassPatches[refName] = SchemaHelpers.Sanitize(name);
                }
                continue;
            }

            if (isExternal)
            {
                // External $ref — the type is generated from its own file.
                // Record a cross-file patch so inheritance is added after all files are generated.
                _context.CrossFileInheritancePatches[typeName] = (SchemaHelpers.Sanitize(name), _context.Namespace);
                continue;
            }

            if (_context.GeneratedTypes.ContainsKey(typeName))
                continue;

            sb.AppendLine();

            var resolvedSchema = schema is OpenApiSchemaReference schemaRef
                ? _resolver.ResolveSchemaFully(schemaRef)
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
                    // For the discriminator property, emit a [JsonIgnore] string property
                    // with the variant's discriminator value as default. System.Text.Json
                    // reserves the discriminator JSON name as metadata — any [JsonPropertyName]
                    // matching it throws InvalidOperationException at runtime.
                    if (string.Equals(propName, discriminatorPropName, StringComparison.Ordinal))
                    {
                        var defaultValue = ExtractDiscriminatorEnumValue(resolvedSchema, propName)
                                           ?? discriminatorValue;
                        EmitDiscriminatorProperty(sb, propName, defaultValue, prefix + "    ");
                        continue;
                    }

                    _propertyGenerator.GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", typeName);
                }
            }

            sb.AppendLine($"{prefix}}}");

            if (resolvedSchema.Properties is not null)
            {
                foreach (var (propName, propSchema) in resolvedSchema.Properties)
                {
                    // Skip inline enum/object generation for the discriminator property
                    // since we emit it as a simple string, not a typed enum.
                    if (string.Equals(propName, discriminatorPropName, StringComparison.Ordinal))
                        continue;

                    GenerateInlineEnums(sb, propName, propSchema, prefix, typeName);
                    GenerateInlineObjects(sb, propName, propSchema, prefix, typeName);
                }
            }
        }
    }

    /// <summary>
    /// Emits a discriminator property as a [JsonIgnore] string with a default value.
    /// 
    /// WHY [JsonIgnore] IS REQUIRED:
    /// System.Text.Json reserves the discriminator property name (e.g., "type") as internal
    /// metadata when [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")] is on the base.
    /// If ANY derived class has a property with [JsonPropertyName("type")], the serializer
    /// throws InvalidOperationException — regardless of whether the base class has a "Type"
    /// property or not. This is a serializer-level constraint, not a C# inheritance issue.
    /// The C# 'new' keyword has no effect on this check.
    ///
    /// We still emit the property (with a default value from the schema's enum) so that:
    /// - The schema's required "type" field is represented in the C# model
    /// - Code can programmatically access the discriminator value via .Type
    /// - No [JsonPropertyName] is emitted (it's meaningless on an ignored property and
    ///   would be contradictory)
    /// - No [Required]/required modifier (the serializer never populates this property)
    /// - No separate single-value enum type (unnecessary for a constant string)
    /// </summary>
    private static void EmitDiscriminatorProperty(StringBuilder sb, string jsonPropName, string? defaultValue, string prefix)
    {
        var csharpName = SchemaHelpers.Sanitize(jsonPropName.ToPascalCase());
        sb.AppendLine($"{prefix}/// <summary>");
        sb.AppendLine($"{prefix}/// Discriminator property. Value is handled by the JSON serializer's polymorphic");
        sb.AppendLine($"{prefix}/// metadata and is not directly serialized/deserialized as a regular property.");
        sb.AppendLine($"{prefix}/// </summary>");
        sb.AppendLine($"{prefix}[JsonIgnore]");
        if (defaultValue is not null)
        {
            sb.AppendLine($"{prefix}public string {csharpName} {{ get; set; }} = \"{defaultValue}\";");
        }
        else
        {
            sb.AppendLine($"{prefix}public string? {csharpName} {{ get; set; }}");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Extracts the actual discriminator enum value from a variant schema's discriminator property.
    /// For example, if the variant has "type": {"enum": ["AnyCrsPoint"]}, returns "AnyCrsPoint".
    /// This ensures [JsonDerivedType] uses the real JSON value rather than the schema title.
    /// </summary>
    private static string? ExtractDiscriminatorEnumValue(IOpenApiSchema variant, string discriminatorPropName)
    {
        if (variant.Properties is null)
            return null;

        if (!variant.Properties.TryGetValue(discriminatorPropName, out var discPropSchema))
            return null;

        var enumValues = discPropSchema.Enum?.OfType<JsonNode>().ToList();
        if (enumValues is { Count: 1 })
            return enumValues[0].ToString();

        return null;
    }

    private void GenerateInlineEnums(StringBuilder sb, string propName, IOpenApiSchema propSchema, string prefix, string parentName)
    {
        if (propSchema is OpenApiSchemaReference)
            return;

        if (propSchema.Enum is { Count: > 0 } && SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
        {
            var enumName = BuildInlineName(parentName, propName);
            sb.AppendLine();
            GenerateEnum(sb, enumName, propSchema, prefix);
            return;
        }

        if (SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
        {
            var itemSchema = propSchema.Items;
            if (itemSchema is not OpenApiSchemaReference && itemSchema.Enum is { Count: > 0 } && SchemaHelpers.HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
            {
                var enumName = BuildInlineName(parentName, propName);
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
                var inlineTypeName = BuildInlineName(parentName, propName);
                sb.AppendLine();

                var baseClass = SchemaHelpers.Sanitize(refs[0].Reference.Id);
                var mergedProperties = new Dictionary<string, IOpenApiSchema>();

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
                var expectedName = SchemaHelpers.Sanitize(BuildInlineName(parentName, propName));
                if (_context.OneOfUnionCache[signature] != expectedName)
                    return;
            }

            var inlineTypeName = (signature is not null ? _context.OneOfUnionCache.GetValueOrDefault(signature) : null)
                                 ?? SchemaHelpers.Sanitize(BuildInlineName(parentName, propName));
            sb.AppendLine();
            GenerateDiscriminatedUnion(sb, inlineTypeName, variants, propSchema.Discriminator, prefix);
            return;
        }

        if (propSchema is not OpenApiSchemaReference
            && SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
            && propSchema.Properties is { Count: > 0 }
            && propSchema.AdditionalProperties is null)
        {
            var inlineTypeName = BuildInlineName(parentName, propName);
            sb.AppendLine();
            GenerateType(sb, inlineTypeName, propSchema, 0);
            return;
        }

        if (SchemaHelpers.HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
        {
            var itemSchema = propSchema.Items;

            if (itemSchema is not OpenApiSchemaReference && itemSchema.AllOf is { Count: > 0 })
            {
                var inlineTypeName = BuildInlineName(parentName, propName);
                sb.AppendLine();
                GenerateType(sb, inlineTypeName, itemSchema, 0);
                return;
            }

            if (itemSchema is not OpenApiSchemaReference
                && SchemaHelpers.HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
                && itemSchema.Properties is { Count: > 0 }
                && itemSchema.AdditionalProperties is null)
            {
                var inlineTypeName = BuildInlineName(parentName, propName);
                sb.AppendLine();
                GenerateType(sb, inlineTypeName, itemSchema, 0);
                return;
            }

            if (itemSchema is not OpenApiSchemaReference
                && (itemSchema.OneOf is { Count: > 0 } || itemSchema.AnyOf is { Count: > 0 }))
            {
                var itemVariants = itemSchema.OneOf is { Count: > 0 } ? itemSchema.OneOf : itemSchema.AnyOf!;

                if (_resolver.HasMeaningfulVariants(itemVariants))
                {
                    var commonBase = _resolver.FindCommonBaseClass(itemVariants);
                    if (commonBase is null)
                    {
                        var signature = SchemaHelpers.GetOneOfSignature(itemVariants);
                        if (signature is not null && _context.OneOfUnionCache.ContainsKey(signature))
                        {
                            var expectedName = SchemaHelpers.Sanitize(BuildInlineName(parentName, propName));
                            if (_context.OneOfUnionCache[signature] != expectedName)
                                return;
                        }

                        var inlineTypeName = (signature is not null ? _context.OneOfUnionCache.GetValueOrDefault(signature) : null)
                                             ?? SchemaHelpers.Sanitize(BuildInlineName(parentName, propName));
                        sb.AppendLine();
                        GenerateDiscriminatedUnion(sb, inlineTypeName, itemVariants, itemSchema.Discriminator, prefix);
                    }
                }
            }
        }
    }
}