//using System.Text;
//using System.Text.Json.Nodes;
//using Microsoft.Extensions.Logging;
//using Microsoft.OpenApi;
//using Osdu.Client.Generator.ConsoleApp.Configuration;
//using Osdu.Client.Generator.ConsoleApp.Extensions;

//namespace Osdu.Client.Generator.ConsoleApp.Generators;

//public class SchemaGenerator
//{
//    private readonly ILogger<SchemaGenerator> _logger;
//    private readonly AppConfiguration _configuration;


//    private readonly OpenApiDocument _document;
//    private readonly string _namespace;
//    private readonly Dictionary<string, string> _generatedTypes = new();

//    public SchemaGenerator(ILogger<SchemaGenerator> logger, AppConfiguration configuration)
//    {
//        _logger = logger;
//        _configuration = configuration;
//    }

//    public void GenerateNew()
//    {
//        _logger.LogInformation("Generating schema code...");

//        if (!Directory.Exists(_configuration.Schema.DefinitionsDir))
//        {
//            _logger.LogError($"No schema generated. Schema definitions directory not found: {_configuration.Schema.DefinitionsDir}");
//            return;
//        }

//        if (!Directory.Exists(_configuration.Schema.OutputDir))
//        {
//            Directory.CreateDirectory(_configuration.Schema.OutputDir);
//        }

//        string[] jsonFiles = Directory.GetFiles(_configuration.Schema.DefinitionsDir, "*.json", SearchOption.AllDirectories);

//        foreach (string jsonFile in jsonFiles)
//        {
//            GenerateInternal(jsonFile);
//        }

//    }

//    private void GenerateInternal(string jsonFile)
//    {
//        var relativePath = Path.GetRelativePath(_configuration.Schema.OutputDir, jsonFile);
//        var relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
//        var outputDir = Path.Combine(_configuration.Schema.OutputDir, relativeDir);
//        Directory.CreateDirectory(outputDir);

//    }

//    /// <summary>
//    /// Scans the input folder (recursively) for *.json schema files,
//    /// wraps each in a minimal OpenAPI document, and generates .NET types
//    /// preserving the folder structure in the output folder.
//    /// </summary>
//    public static void GenerateFromJsonSchemaFolder(string inputFolder, string outputFolder, string baseNamespace = "Generated.Models")
//    {
//        if (!Directory.Exists(inputFolder))
//        {
//            ConsoleEx.WriteRed($"Input folder not found: {inputFolder}");
//            return;
//        }

//        Directory.CreateDirectory(outputFolder);

//        var jsonFiles = Directory.GetFiles(inputFolder, "*.json", SearchOption.AllDirectories);

//        if (jsonFiles.Length == 0)
//        {
//            Console.WriteLine("No JSON schema files found.");
//            return;
//        }

//        foreach (var file in jsonFiles)
//        {
//            var relativePath = Path.GetRelativePath(inputFolder, file);
//            var relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
//            var outputDir = Path.Combine(outputFolder, relativeDir);
//            Directory.CreateDirectory(outputDir);

//            var schemaName = Path.GetFileNameWithoutExtension(file);
//            //var subNamespace = string.IsNullOrEmpty(relativeDir)
//            //    ? baseNamespace
//            //    : $"{baseNamespace}.{relativeDir.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.')}";
//            var subNamespace = baseNamespace;


//            try
//            {
//                var json = File.ReadAllText(file);

//                // Wrap the JSON schema in a minimal OpenAPI document so we can reuse TypeGenerator
//                var wrappedJson = $$"""
//                        {
//                            "openapi": "3.0.0",
//                            "info": { "title": "{{schemaName}}", "version": "1.0.0" },
//                            "paths": {},
//                            "components": {
//                                "schemas": {
//                                    "{{schemaName}}": {{json}}
//                                }
//                            }
//                        }
//                        """;

//                var result = OpenApiDocument.Parse(wrappedJson, "json");

//                if (result.Document is null)
//                {
//                    Console.WriteLine($"  Skipped (parse error): {relativePath}");
//                    continue;
//                }

//                //var generator = new Schema(result.Document, subNamespace);
//                //generator.Generate(outputDir);
//                Console.WriteLine($"  Generated from schema: {relativePath}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"  Error processing {relativePath}: {ex.Message}");
//            }
//        }
//    }

//    public void Generate(string outputFolder)
//    {
//        Directory.CreateDirectory(outputFolder);

//        var schemas = _document.Components?.Schemas;
//        if (schemas is null || schemas.Count == 0)
//        {
//            Console.WriteLine("No schemas found in document.");
//            return;
//        }

//        foreach (var (name, schema) in schemas)
//        {
//            var code = GenerateFileForSchema(name, schema);
//            _generatedTypes[name] = code;
//        }

//        // Write all files
//        foreach (var (name, code) in _generatedTypes)
//        {
//            File.WriteAllText(Path.Combine(outputFolder, $"{name}.cs"), code);
//            Console.WriteLine($"  Generated: {name}.cs");
//        }
//    }

//    public string GenerateAll()
//    {
//        var sb = new StringBuilder();
//        sb.AppendLine("// <auto-generated/>");
//        sb.AppendLine("#nullable enable");
//        sb.AppendLine();
//        sb.AppendLine("using System;");
//        sb.AppendLine("using System.Collections.Generic;");
//        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
//        sb.AppendLine("using System.Text.Json.Serialization;");
//        sb.AppendLine();
//        sb.AppendLine($"namespace {_namespace};");
//        sb.AppendLine();

//        var schemas = _document.Components?.Schemas;
//        if (schemas is null) return sb.ToString();

//        foreach (var (name, schema) in schemas)
//        {
//            GenerateType(sb, name, schema, indent: 0);
//            sb.AppendLine();
//        }

//        return sb.ToString();
//    }

//    private string GenerateFileForSchema(string name, IOpenApiSchema schema)
//    {
//        var sb = new StringBuilder();
//        sb.AppendLine("// <auto-generated/>");
//        sb.AppendLine("#nullable enable");
//        sb.AppendLine();
//        sb.AppendLine("using System;");
//        sb.AppendLine("using System.Collections.Generic;");
//        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
//        sb.AppendLine("using System.Text.Json.Serialization;");
//        sb.AppendLine();
//        sb.AppendLine($"namespace {_namespace};");
//        sb.AppendLine();

//        GenerateType(sb, name, schema, indent: 0);

//        return sb.ToString();
//    }

//    private void GenerateType(StringBuilder sb, string name, IOpenApiSchema schema, int indent)
//    {
//        var prefix = new string(' ', indent * 4);

//        if (schema.Enum is { Count: > 0 })
//        {
//            GenerateEnum(sb, name, schema, prefix);
//            return;
//        }

//        // Handle oneOf/anyOf as discriminated union via abstract base + derived
//        if (schema.OneOf is { Count: > 0 })
//        {
//            GenerateDiscriminatedUnion(sb, name, schema.OneOf, schema.Discriminator, prefix);
//            return;
//        }

//        if (schema.AnyOf is { Count: > 0 })
//        {
//            GenerateDiscriminatedUnion(sb, name, schema.AnyOf, schema.Discriminator, prefix);
//            return;
//        }

//        // Handle allOf (inheritance / composition)
//        var (baseClass, allProperties) = ResolveAllOf(schema);

//        // Generate class
//        if (schema.Description is not null)
//        {
//            AppendSummary(sb, schema.Description, prefix);
//        }

//        var inheritance = baseClass is not null ? $" : {baseClass}" : "";
//        sb.AppendLine($"{prefix}public class {Sanitize(name)}{inheritance}");
//        sb.AppendLine($"{prefix}{{");

//        var properties = allProperties.Count > 0 ? allProperties : schema.Properties;
//        var required = schema.Required ?? new HashSet<string>();

//        if (properties is not null)
//        {
//            foreach (var (propName, propSchema) in properties)
//            {
//                GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", name);
//            }
//        }

//        sb.AppendLine($"{prefix}}}");

//        // Generate inline types for properties (enums and nested objects)
//        if (properties is not null)
//        {
//            foreach (var (propName, propSchema) in properties)
//            {
//                GenerateInlineEnums(sb, propName, propSchema, prefix, name);
//                GenerateInlineObjects(sb, propName, propSchema, prefix, name);
//            }
//        }
//    }

//    private void GenerateDiscriminatedUnion(
//  StringBuilder sb,
//  string name,
//  IList<IOpenApiSchema> variants,
//  OpenApiDiscriminator? discriminator,
//  string prefix)
//    {
//        var resolvedVariants = new List<(string TypeName, string DiscriminatorValue, IOpenApiSchema Schema)>();
//        int inlineIndex = 0;

//        foreach (var variant in variants)
//        {
//            var refName = GetSchemaReferenceName(variant);
//            if (refName is not null)
//            {
//                resolvedVariants.Add((Sanitize(refName.ToPascalCase()), refName, variant));
//            }
//            else
//            {
//                // Try to get a title or generate a name
//                var title = variant.Title;
//                string typeName;
//                string discriminatorValue;

//                if (!string.IsNullOrEmpty(title))
//                {
//                    typeName = $"{Sanitize(name)}{Sanitize(title.ToPascalCase())}";
//                    discriminatorValue = title;
//                }
//                else
//                {
//                    typeName = $"{Sanitize(name)}Variant{++inlineIndex}";
//                    discriminatorValue = typeName;
//                }

//                resolvedVariants.Add((typeName, discriminatorValue, variant));
//            }
//        }

//        if (resolvedVariants.Count == 0)
//        {
//            sb.AppendLine($"{prefix}public class {Sanitize(name)}");
//            sb.AppendLine($"{prefix}{{");
//            sb.AppendLine($"{prefix}}}");
//            return;
//        }

//        // Generate abstract base with polymorphic attributes
//        sb.AppendLine($"{prefix}[JsonPolymorphic(TypeDiscriminatorPropertyName = \"{discriminator?.PropertyName ?? "type"}\")]");

//        foreach (var (typeName, discriminatorValue, _) in resolvedVariants)
//        {
//            sb.AppendLine($"{prefix}[JsonDerivedType(typeof({typeName}), \"{discriminatorValue}\")]");
//        }

//        sb.AppendLine($"{prefix}public abstract class {Sanitize(name)}");
//        sb.AppendLine($"{prefix}{{");
//        sb.AppendLine($"{prefix}}}");

//        // Generate variant classes — both inline and referenced (internal) schemas
//        foreach (var (typeName, _, schema) in resolvedVariants)
//        {
//            // Skip if this type is already generated as a top-level schema in _generatedTypes
//            if (_generatedTypes.ContainsKey(typeName))
//                continue;

//            sb.AppendLine();

//            var resolvedSchema = schema is OpenApiSchemaReference schemaRef
//                ? ResolveReference(schemaRef)
//                : schema;

//            if (resolvedSchema is null)
//                continue;

//            if (resolvedSchema.Description is not null)
//            {
//                AppendSummary(sb, resolvedSchema.Description, prefix);
//            }

//            sb.AppendLine($"{prefix}public class {typeName} : {Sanitize(name)}");
//            sb.AppendLine($"{prefix}{{");

//            if (resolvedSchema.Properties is not null)
//            {
//                var required = resolvedSchema.Required ?? new HashSet<string>();
//                foreach (var (propName, propSchema) in resolvedSchema.Properties)
//                {
//                    GenerateProperty(sb, propName, propSchema, required.Contains(propName), prefix + "    ", typeName);
//                }
//            }

//            sb.AppendLine($"{prefix}}}");

//            // Generate inline enums and objects for variant properties
//            if (resolvedSchema.Properties is not null)
//            {
//                foreach (var (propName, propSchema) in resolvedSchema.Properties)
//                {
//                    GenerateInlineEnums(sb, propName, propSchema, prefix, typeName);
//                    GenerateInlineObjects(sb, propName, propSchema, prefix, typeName);
//                }
//            }
//        }
//    }

//    private void GenerateEnum(StringBuilder sb, string name, IOpenApiSchema schema, string prefix)
//    {
//        if (schema.Description is not null)
//        {
//            AppendSummary(sb, schema.Description, prefix);
//        }

//        sb.AppendLine($"{prefix}[JsonConverter(typeof(JsonStringEnumConverter))]");
//        sb.AppendLine($"{prefix}public enum {Sanitize(name)}");
//        sb.AppendLine($"{prefix}{{");

//        var usedNames = new HashSet<string>(StringComparer.Ordinal);

//        foreach (var value in schema.Enum?.OfType<JsonNode>() ?? [])
//        {
//            var stringValue = value.ToString();
//            var memberName = Sanitize(stringValue.ToPascalCase());

//            // Handle duplicates caused by case-insensitive collisions (e.g., "f" and "F")
//            if (!usedNames.Add(memberName))
//            {
//                // Differentiate by appending "Lower" or "Upper" or the raw value
//                memberName = Sanitize(stringValue.ToPascalCase() + "_" + (char.IsLower(stringValue[0]) ? "Lower" : "Upper"));
//                usedNames.Add(memberName);
//            }

//            sb.AppendLine($"{prefix}    [JsonStringEnumMemberName(\"{stringValue}\")]");
//            sb.AppendLine($"{prefix}    {memberName},");
//            sb.AppendLine();
//        }

//        sb.AppendLine($"{prefix}}}");
//    }

//    private void GenerateInlineEnums(StringBuilder sb, string propName, IOpenApiSchema propSchema, string prefix, string parentName)
//    {
//        // Direct enum on the property
//        if (propSchema.Enum is { Count: > 0 } && HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
//        {
//            var enumName = $"{parentName}{propName.ToPascalCase()}";
//            sb.AppendLine();
//            GenerateEnum(sb, enumName, propSchema, prefix);
//            return;
//        }

//        // Enum on array items
//        if (HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
//        {
//            var itemSchema = propSchema.Items;
//            if (itemSchema is not OpenApiSchemaReference && itemSchema.Enum is { Count: > 0 } && HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.String))
//            {
//                var enumName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();
//                GenerateEnum(sb, enumName, itemSchema, prefix);
//            }
//        }
//    }

//    private void GenerateInlineObjects(StringBuilder sb, string propName, IOpenApiSchema propSchema, string prefix, string parentName)
//    {
//        // Handle allOf with multiple refs — generate a composed class merging all properties
//        if (propSchema.AllOf is { Count: > 0 })
//        {
//            var refs = propSchema.AllOf.OfType<OpenApiSchemaReference>().ToList();
//            var inlineSchemas = propSchema.AllOf.Where(s => s is not OpenApiSchemaReference).ToList();

//            if (refs.Count > 1 || (refs.Count == 1 && inlineSchemas.Any(s => s.Properties is { Count: > 0 })))
//            {
//                var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();

//                // Use first $ref as base class, merge all others' properties
//                var baseClass = Sanitize(refs[0].Reference.Id);
//                var mergedProperties = new Dictionary<string, IOpenApiSchema>();

//                // Collect properties from non-first refs (we can't do multiple inheritance in C#)
//                foreach (var refSchema in refs.Skip(1))
//                {
//                    if (refSchema.Properties is not null)
//                    {
//                        foreach (var (key, value) in refSchema.Properties)
//                            mergedProperties.TryAdd(key, value);
//                    }
//                }

//                // Collect properties from inline schemas
//                foreach (var inlineSchema in inlineSchemas)
//                {
//                    if (inlineSchema.Properties is not null)
//                    {
//                        foreach (var (key, value) in inlineSchema.Properties)
//                            mergedProperties.TryAdd(key, value);
//                    }
//                }

//                // Generate the composed class
//                if (propSchema.Description is not null)
//                {
//                    AppendSummary(sb, propSchema.Description, prefix);
//                }

//                // Build interfaces list from additional refs for documentation
//                var additionalBases = refs.Skip(1).Select(r => Sanitize(r.Reference.Id)).ToList();
//                var commentSuffix = additionalBases.Count > 0
//                    ? $" // Also composes: {string.Join(", ", additionalBases)}"
//                    : "";

//                sb.AppendLine($"{prefix}public class {Sanitize(inlineTypeName)} : {baseClass}{commentSuffix}");
//                sb.AppendLine($"{prefix}{{");

//                foreach (var (key, value) in mergedProperties)
//                {
//                    GenerateProperty(sb, key, value, false, prefix + "    ", inlineTypeName);
//                }

//                sb.AppendLine($"{prefix}}}");

//                // Generate inline enums and objects for the composed class properties
//                foreach (var (key, value) in mergedProperties)
//                {
//                    GenerateInlineEnums(sb, key, value, prefix, inlineTypeName);
//                    GenerateInlineObjects(sb, key, value, prefix, inlineTypeName);
//                }

//                return;
//            }
//        }

//        // Handle oneOf/anyOf — generate abstract base with derived types if discriminator present
//        if (propSchema.OneOf is { Count: > 0 } || propSchema.AnyOf is { Count: > 0 })
//        {
//            var variants = propSchema.OneOf is { Count: > 0 } ? propSchema.OneOf : propSchema.AnyOf!;
//            var refs = variants.OfType<OpenApiSchemaReference>().ToList();

//            if (refs.Count > 0 && propSchema.Discriminator is not null)
//            {
//                var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();
//                GenerateDiscriminatedUnion(sb, inlineTypeName, variants, propSchema.Discriminator, prefix);
//                return;
//            }
//        }

//        // Inline object directly on the property
//        if (propSchema is not OpenApiSchemaReference
//            && HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
//            && propSchema.Properties is { Count: > 0 }
//            && propSchema.AdditionalProperties is null)
//        {
//            var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//            sb.AppendLine();
//            GenerateType(sb, inlineTypeName, propSchema, 0);
//            return;
//        }

//        // Inline object on array items
//        if (HasFlag(propSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Array) && propSchema.Items is not null)
//        {
//            var itemSchema = propSchema.Items;

//            // Handle allOf on array items
//            if (itemSchema is not OpenApiSchemaReference && itemSchema.AllOf is { Count: > 0 })
//            {
//                var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();
//                GenerateType(sb, inlineTypeName, itemSchema, 0);
//                return;
//            }

//            // Handle inline object on array items
//            if (itemSchema is not OpenApiSchemaReference
//                && HasFlag(itemSchema.Type ?? JsonSchemaType.Null, JsonSchemaType.Object)
//                && itemSchema.Properties is { Count: > 0 }
//                && itemSchema.AdditionalProperties is null)
//            {
//                var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();
//                GenerateType(sb, inlineTypeName, itemSchema, 0);
//                return;
//            }

//            // Handle oneOf/anyOf on array items
//            if (itemSchema is not OpenApiSchemaReference
//                && (itemSchema.OneOf is { Count: > 0 } || itemSchema.AnyOf is { Count: > 0 }))
//            {
//                var variants = itemSchema.OneOf is { Count: > 0 } ? itemSchema.OneOf : itemSchema.AnyOf!;
//                var inlineTypeName = $"{parentName}{propName.ToPascalCase()}";
//                sb.AppendLine();
//                GenerateDiscriminatedUnion(sb, inlineTypeName, variants, itemSchema.Discriminator, prefix);
//                return;
//            }
//        }
//    }

//    private void GenerateProperty(
//        StringBuilder sb,
//        string propName,
//        IOpenApiSchema propSchema,
//        bool isRequired,
//        string prefix,
//        string parentName)
//    {
//        if (propSchema.Description is not null)
//        {
//            AppendSummary(sb, propSchema.Description, prefix);
//        }

//        // Validation attributes
//        GenerateValidationAttributes(sb, propSchema, isRequired, prefix);

//        sb.AppendLine($"{prefix}[JsonPropertyName(\"{propName}\")]");

//        var typeName = ResolveTypeName(propSchema, parentName, propName);
//        var nullable = !isRequired && IsNullableType(propSchema) ? "?" : "";
//        var csharpName = propName.ToPascalCase();

//        sb.AppendLine($"{prefix}public {typeName}{nullable} {Sanitize(csharpName)} {{ get; set; }}");
//        sb.AppendLine();
//    }

//    private void GenerateValidationAttributes(StringBuilder sb, IOpenApiSchema schema, bool isRequired, string prefix)
//    {
//        if (isRequired)
//            sb.AppendLine($"{prefix}[Required]");

//        if (schema.MinLength.HasValue)
//            sb.AppendLine($"{prefix}[MinLength({schema.MinLength.Value})]");

//        if (schema.MaxLength.HasValue)
//            sb.AppendLine($"{prefix}[MaxLength({schema.MaxLength.Value})]");

//        if (schema.Minimum is not null)
//        {
//            var max = schema.Maximum is not null ? schema.Maximum : "double.MaxValue";
//            sb.AppendLine($"{prefix}[Range({schema.Minimum}, {max})]");
//        }

//        if (schema.Pattern is not null)
//            sb.AppendLine($"{prefix}[RegularExpression(@\"{schema.Pattern}\")]");

//        if (schema.MinItems.HasValue)
//            sb.AppendLine($"{prefix}[MinLength({schema.MinItems.Value})]");

//        if (schema.MaxItems.HasValue)
//            sb.AppendLine($"{prefix}[MaxLength({schema.MaxItems.Value})]");
//    }

//    private string ResolveTypeName(IOpenApiSchema schema, string parentName, string propertyName)
//    {
//        // $ref — check if this is a reference to a named schema
//        if (schema is OpenApiSchemaReference schemaRef)
//            return Sanitize(schemaRef.Reference.Id);

//        // oneOf / anyOf at property level — use first ref or generate a marker interface
//        if (schema.OneOf is { Count: > 0 })
//        {
//            var firstRef = schema.OneOf.OfType<OpenApiSchemaReference>().FirstOrDefault();
//            if (firstRef is not null)
//                return Sanitize(firstRef.Reference.Id);
//            return "object";
//        }

//        if (schema.AnyOf is { Count: > 0 })
//        {
//            var firstRef = schema.AnyOf.OfType<OpenApiSchemaReference>().FirstOrDefault();
//            if (firstRef is not null)
//                return Sanitize(firstRef.Reference.Id);
//            return "object";
//        }

//        // allOf at property level — if multiple $refs, generate a composed inline type
//        if (schema.AllOf is { Count: > 0 })
//        {
//            var refs = schema.AllOf.OfType<OpenApiSchemaReference>().ToList();
//            if (refs.Count > 1)
//            {
//                // Multiple base schemas — generate a composed inline class name
//                return Sanitize($"{parentName}{propertyName.ToPascalCase()}");
//            }
//            if (refs.Count == 1)
//            {
//                // Single ref with possible additional inline properties
//                var inlineProps = schema.AllOf
//                    .Where(s => s is not OpenApiSchemaReference && s.Properties is { Count: > 0 })
//                    .SelectMany(s => s.Properties)
//                    .ToList();

//                if (inlineProps.Count > 0)
//                {
//                    // Has extra properties beyond the ref — generate composed type
//                    return Sanitize($"{parentName}{propertyName.ToPascalCase()}");
//                }

//                return Sanitize(refs[0].Reference.Id);
//            }
//        }

//        var type = schema.Type ?? JsonSchemaType.Null;
//        var format = schema.Format;

//        if (HasFlag(type, JsonSchemaType.String))
//        {
//            if (schema.Enum is { Count: > 0 })
//                return Sanitize($"{parentName}{propertyName.ToPascalCase()}");
//            return format switch
//            {
//                "date-time" => "DateTimeOffset",
//                "date" => "DateOnly",
//                "time" => "TimeOnly",
//                "uuid" => "Guid",
//                "uri" => "Uri",
//                "binary" => "byte[]",
//                _ => "string"
//            };
//        }

//        if (HasFlag(type, JsonSchemaType.Integer))
//            return format == "int64" ? "long" : "int";

//        if (HasFlag(type, JsonSchemaType.Number))
//            return format switch
//            {
//                "float" => "float",
//                "decimal" => "decimal",
//                _ => "double"
//            };

//        if (HasFlag(type, JsonSchemaType.Boolean))
//            return "bool";

//        if (HasFlag(type, JsonSchemaType.Array))
//        {
//            var itemSchema = schema.Items;
//            var itemType = itemSchema is not null ? ResolveTypeName(itemSchema, parentName, propertyName) : "object";
//            return $"List<{itemType}>";
//        }

//        if (HasFlag(type, JsonSchemaType.Object))
//        {
//            if (schema.AdditionalProperties is not null)
//                return $"Dictionary<string, {ResolveTypeName(schema.AdditionalProperties, parentName, propertyName)}>";
//            if (schema.Properties?.Count > 0)
//                return Sanitize($"{parentName}{propertyName.ToPascalCase()}");
//        }

//        return "object";
//    }

//    private (string? BaseClass, IDictionary<string, IOpenApiSchema> Properties) ResolveAllOf(IOpenApiSchema schema)
//    {
//        if (schema.AllOf is not { Count: > 0 })
//            return (null, schema.Properties ?? new Dictionary<string, IOpenApiSchema>());

//        string? baseClass = null;
//        var mergedProperties = new Dictionary<string, IOpenApiSchema>();

//        foreach (var allOfItem in schema.AllOf)
//        {
//            if (allOfItem is OpenApiSchemaReference schemaRef)
//            {
//                baseClass = Sanitize(schemaRef.Reference.Id);
//            }
//            else if (allOfItem.Properties is not null)
//            {
//                foreach (var (key, value) in allOfItem.Properties)
//                {
//                    mergedProperties.TryAdd(key, value);
//                }
//            }
//        }

//        // Also add direct properties
//        if (schema.Properties is not null)
//        {
//            foreach (var (key, value) in schema.Properties)
//            {
//                mergedProperties.TryAdd(key, value);
//            }
//        }

//        return (baseClass, mergedProperties);
//    }

//    private IOpenApiSchema? ResolveReference(OpenApiSchemaReference schemaRef)
//    {
//        var id = schemaRef.Reference.Id;
//        // Try to find in document's component schemas
//        if (_document.Components?.Schemas is not null &&
//            _document.Components.Schemas.TryGetValue(id, out var resolved))
//        {
//            return resolved;
//        }

//        // If the reference itself has properties, use it directly
//        if (schemaRef.Properties is { Count: > 0 })
//            return schemaRef;

//        return schemaRef;
//    }

//    private static string? GetSchemaReferenceName(IOpenApiSchema schema)
//        => schema is OpenApiSchemaReference schemaRef ? schemaRef.Reference.Id : null;

//    private static bool HasFlag(JsonSchemaType type, JsonSchemaType flag)
//        => (type & flag) == flag;

//    private static bool IsNullableType(IOpenApiSchema schema)
//    {
//        var type = schema is OpenApiSchemaReference ? JsonSchemaType.Object : (schema.Type ?? JsonSchemaType.Null);
//        if (HasFlag(type, JsonSchemaType.Null)) return true;
//        return !HasFlag(type, JsonSchemaType.String) && !HasFlag(type, JsonSchemaType.Array);
//    }

//    private static string Sanitize(string? name)
//    {
//        if (string.IsNullOrEmpty(name)) return "Unknown";
//        var result = new StringBuilder();
//        foreach (var ch in name)
//        {
//            if (char.IsLetterOrDigit(ch) || ch == '_')
//                result.Append(ch);
//        }
//        if (result.Length > 0 && char.IsDigit(result[0]))
//            result.Insert(0, '_');
//        return result.ToString().Replace("json", "");
//    }

//    private static string EscapeXml(string text)
//        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

//    private static void AppendSummary(StringBuilder sb, string? description, string prefix)
//    {
//        if (description is null) return;

//        sb.AppendLine($"{prefix}/// <summary>");
//        foreach (var line in description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
//        {
//            sb.AppendLine($"{prefix}/// {EscapeXml(line.Trim())}");
//        }
//        sb.AppendLine($"{prefix}/// </summary>");
//    }
//}
