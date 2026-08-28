using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

public class SchemaGenerator
{
    private readonly ILogger<SchemaGenerator> _logger;
    private readonly AppConfiguration _configuration;

    private readonly SchemaGeneratorContext _context = new();
    private SchemaResolver _resolver = null!;
    private TypeNameResolver _typeNameResolver = null!;
    private PropertyGenerator _propertyGenerator = null!;
    private TypeGenerator _typeGenerator = null!;

    public SchemaGenerator(ILogger<SchemaGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private void InitializeComponents()
    {
        _resolver = new SchemaResolver(_context);
        _typeNameResolver = new TypeNameResolver(_resolver, _context);
        _propertyGenerator = new PropertyGenerator(_typeNameResolver);
        _typeGenerator = new TypeGenerator(_context, _resolver, _typeNameResolver, _propertyGenerator);
    }

    public void GenerateNew(string jsonFile, string outputDir, string baseNamespace, bool hasOpenApiHeader = true)
    {
        string schemaName = Path.GetFileNameWithoutExtension(jsonFile).Replace('.', '_');
        string parentName = Path.GetFileName(Path.GetDirectoryName(jsonFile) ?? string.Empty).ToPascalCase();
        string schemaNamespace = $"{baseNamespace}";

        string jsonContent = File.ReadAllText(jsonFile);
        if (!hasOpenApiHeader)
        {
            jsonContent = AddOpenApiHeader(jsonContent, schemaName);
        }

        ReadResult? result = OpenApiDocument.Parse(jsonContent, "json");
        OpenApiDocument? openApiDocument = result?.Document;
        _context.Document = openApiDocument!;
        _context.Namespace = schemaNamespace;
        _context.JsonFilePath = jsonFile;
        _context.Reset();

        InitializeComponents();

        if (openApiDocument == null)
        {
            _logger.LogWarning($"  Failed to parse OpenAPI document from definition file: {jsonFile}");
            return;
        }

        Directory.CreateDirectory(outputDir);

        IDictionary<string, IOpenApiSchema>? schemas = _context.Document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            _logger.LogWarning($"No schemas found in definition file: {jsonFile}");
            return;
        }

        foreach (var (name, schema) in schemas)
        {
            var code = GenerateFileForSchema(name, schema);
            _context.GeneratedTypes[name] = code;
        }

        // FIX: Apply pending base class patches for internal $ref variants that were
        // registered before their target schema was generated in the same file
        ApplyPendingInternalPatches();

        // Apply any cross-file inheritance patches recorded by earlier files
        ApplyPendingCrossFilePatches();

        // Post-process: add [JsonIgnore] to properties in derived types that conflict
        // with the polymorphic type discriminator property name.
        FixDiscriminatorPropertyConflicts();

        foreach (var (name, code) in _context.GeneratedTypes)
        {
            string outputFile = Path.Combine(outputDir, $"{MakeName(name)}.cs");
            File.WriteAllText(outputFile, code);
            _logger.LogInformation($"    Generated schema: {MakeName(name)}.cs");
        }
    }

    /// <summary>
    /// Applies pending base class patches for internal $ref variants within the same file.
    /// These are registered when GenerateDiscriminatedUnion encounters an internal $ref
    /// variant that hasn't been generated yet at that point.
    /// </summary>
    private void ApplyPendingInternalPatches()
    {
        foreach (var (schemaName, baseClassName) in _context.PendingBaseClassPatches)
        {
            if (_context.GeneratedTypes.TryGetValue(schemaName, out var code))
            {
                var typeName = SchemaHelpers.Sanitize(schemaName);
                _context.GeneratedTypes[schemaName] = SchemaHelpers.PatchClassInheritance(code, typeName, baseClassName);
            }
        }
    }

    /// <summary>
    /// Applies cross-file inheritance patches to the current file's generated types.
    /// Called during GenerateNew for types processed after the base was generated.
    /// </summary>
    private void ApplyPendingCrossFilePatches()
    {
        foreach (var (typeName, (baseClassName, baseNamespace)) in _context.CrossFileInheritancePatches)
        {
            foreach (var key in _context.GeneratedTypes.Keys.ToList())
            {
                if (SchemaHelpers.Sanitize(key) == typeName)
                {
                    var code = _context.GeneratedTypes[key];
                    code = SchemaHelpers.PatchClassInheritanceWithUsing(code, typeName, baseClassName,
                        _context.Namespace != baseNamespace ? baseNamespace : null);
                    _context.GeneratedTypes[key] = code;
                }
            }
        }
    }

    /// <summary>
    /// Post-processes already-written files to apply any remaining cross-file inheritance patches.
    /// Call this after all schema files have been generated.
    /// </summary>
    public void ApplyCrossFilePatches(string outputBaseDir)
    {
        if (_context.CrossFileInheritancePatches.Count == 0)
            return;

        _logger.LogInformation("Applying cross-file inheritance patches...");

        var csFiles = Directory.GetFiles(outputBaseDir, "*.cs", SearchOption.AllDirectories);

        foreach (var (typeName, (baseClassName, baseNamespace)) in _context.CrossFileInheritancePatches)
        {
            foreach (var csFile in csFiles)
            {
                var code = File.ReadAllText(csFile);
                if (!code.Contains($"public class {typeName}"))
                    continue;

                if (code.Contains($"public class {typeName} :"))
                    continue;

                var currentNamespace = ExtractNamespace(code);
                code = SchemaHelpers.PatchClassInheritanceWithUsing(code, typeName, baseClassName,
                    currentNamespace != baseNamespace ? baseNamespace : null);

                File.WriteAllText(csFile, code);
                _logger.LogInformation($"    Patched {Path.GetFileName(csFile)}: {typeName} now inherits {baseClassName}");
            }
        }
    }

    private static string? ExtractNamespace(string code)
    {
        var match = Regex.Match(code, @"namespace\s+([\w.]+)\s*;");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Finds polymorphic base classes that use [JsonPolymorphic(TypeDiscriminatorPropertyName = "X" ...)]
    /// and adds [JsonIgnore] to any property in their derived classes whose [JsonPropertyName] matches
    /// the discriminator property name. This prevents System.Text.Json from throwing
    /// InvalidOperationException at runtime.
    /// </summary>
    private void FixDiscriminatorPropertyConflicts()
    {
        // Pattern to find: [JsonPolymorphic(TypeDiscriminatorPropertyName = "type" ...)]
        // Must NOT require ")" immediately after the discriminator value — there may be
        // additional parameters like UnknownDerivedTypeHandling.
        var polymorphicPattern = new Regex(
            @"\[JsonPolymorphic\(TypeDiscriminatorPropertyName\s*=\s*""(?<disc>[^""]+)""");

        // Pattern to find derived type class names from [JsonDerivedType(typeof(ClassName), ...)]
        var derivedTypePattern = new Regex(
            @"\[JsonDerivedType\(typeof\((?<typeName>[^)]+)\)");

        // Collect discriminator info: for each base class, find the discriminator name and derived type names
        var discriminatorsByDerivedType = new Dictionary<string, string>();

        foreach (var (name, code) in _context.GeneratedTypes)
        {
            var polyMatch = polymorphicPattern.Match(code);
            if (!polyMatch.Success)
                continue;

            string discriminatorPropertyName = polyMatch.Groups["disc"].Value;

            var derivedMatches = derivedTypePattern.Matches(code);
            foreach (Match derivedMatch in derivedMatches)
            {
                string derivedTypeName = derivedMatch.Groups["typeName"].Value;
                discriminatorsByDerivedType[derivedTypeName] = discriminatorPropertyName;
            }
        }

        // Now fix derived types: add [JsonIgnore] to the conflicting property
        var keys = _context.GeneratedTypes.Keys.ToList();
        foreach (var name in keys)
        {
            string code = _context.GeneratedTypes[name];
            bool modified = false;

            foreach (var (derivedTypeName, discriminatorName) in discriminatorsByDerivedType)
            {
                if (!code.Contains($"class {derivedTypeName}"))
                    continue;

                // Only apply if this class actually inherits from a polymorphic base
                var classPattern = new Regex($@"class\s+{Regex.Escape(derivedTypeName)}\s*:\s*\w+");
                if (!classPattern.IsMatch(code))
                    continue;

                string propertyNameAttr = $"[JsonPropertyName(\"{discriminatorName}\")]";
                if (!code.Contains(propertyNameAttr))
                    continue;

                // Already has [JsonIgnore] for this property — skip
                // Check within the class body specifically
                var classIdx = code.IndexOf($"class {derivedTypeName}", StringComparison.Ordinal);
                if (classIdx < 0) continue;
                var propIdx = code.IndexOf(propertyNameAttr, classIdx, StringComparison.Ordinal);
                if (propIdx < 0) continue;

                // Check if [JsonIgnore] is already right before this [JsonPropertyName]
                int checkStart = Math.Max(classIdx, propIdx - 100);
                string preceding = code[checkStart..propIdx];
                if (preceding.Contains("[JsonIgnore]"))
                    continue;

                // Case 1: [Required]\n    [JsonPropertyName("type")]
                var withRequiredPattern = new Regex(
                    @"(?<indent>[ \t]*)\[Required\]\s*\r?\n(?<indent2>[ \t]*)" +
                    Regex.Escape(propertyNameAttr));

                var withRequiredMatch = withRequiredPattern.Match(code, classIdx);
                if (withRequiredMatch.Success && withRequiredMatch.Index >= classIdx)
                {
                    string indent2 = withRequiredMatch.Groups["indent2"].Value;
                    code = code[..withRequiredMatch.Index] +
                           $"{indent2}[JsonIgnore]\n{indent2}{propertyNameAttr}" +
                           code[(withRequiredMatch.Index + withRequiredMatch.Length)..];
                    modified = true;
                }
                else
                {
                    // Case 2: No [Required] — just [JsonPropertyName("type")]
                    var withoutRequiredPattern = new Regex(
                        @"(?<indent>[ \t]*)" + Regex.Escape(propertyNameAttr));

                    var withoutRequiredMatch = withoutRequiredPattern.Match(code, classIdx);
                    if (withoutRequiredMatch.Success && withoutRequiredMatch.Index >= classIdx)
                    {
                        string indent = withoutRequiredMatch.Groups["indent"].Value;
                        code = code[..withoutRequiredMatch.Index] +
                               $"{indent}[JsonIgnore]\n{indent}{propertyNameAttr}" +
                               code[(withoutRequiredMatch.Index + withoutRequiredMatch.Length)..];
                        modified = true;
                    }
                }

                // Remove 'required' modifier from the discriminator property line
                var csharpPropName = SchemaHelpers.Sanitize(discriminatorName.ToPascalCase());
                var requiredModifierPattern = new Regex(
                    @"(public\s+)required(\s+\S+\s+" + Regex.Escape(csharpPropName) + @"\s*\{)");
                var reqMatch = requiredModifierPattern.Match(code, classIdx);
                if (reqMatch.Success)
                {
                    code = code[..reqMatch.Index] +
                           reqMatch.Groups[1].Value + reqMatch.Groups[2].Value +
                           code[(reqMatch.Index + reqMatch.Length)..];
                    modified = true;
                }
            }

            if (modified)
            {
                _context.GeneratedTypes[name] = code;
            }
        }
    }


    private string AddOpenApiHeader(string jsonContent, string schemaName)
    {
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
        return wrappedJson;
    }

    private string MakeName(string name)
    {
        return name.Replace('-', '_')
            .Replace(' ', '_')
            .Replace('.', '_');
    }

    private void BuildUsingsAndNamespace(StringBuilder sb, string schemaNamespace, IEnumerable<string> additionalUsings)
    {
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using Osdu.Client.Converters;");

        foreach (var ns in additionalUsings)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {schemaNamespace};");
        sb.AppendLine();
    }

    private string GenerateFileForSchema(string name, IOpenApiSchema schema)
    {
        StringBuilder sb = new StringBuilder();

        CodeGenerator.BuildAutogenComment(sb);

        // Set the root schema name so all inline types are anchored to it
        _context.RootSchemaName = SchemaHelpers.Sanitize(name);

        var referencedNamespaces = CollectExternalNamespaces(schema);
        BuildUsingsAndNamespace(sb, _context.Namespace, referencedNamespaces);

        _typeGenerator.GenerateType(sb, name, schema, indent: 0);

        return sb.ToString();
    }

    /// <summary>
    /// Walks the schema to find all external $ref references and computes
    /// the namespaces they belong to based on their file paths relative to
    /// the schema definitions directory.
    /// </summary>
    private HashSet<string> CollectExternalNamespaces(IOpenApiSchema schema)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance);
        CollectExternalNamespacesRecursive(schema, namespaces, visited);
        namespaces.Remove(_context.Namespace);
        return namespaces;
    }

    private void CollectExternalNamespacesRecursive(IOpenApiSchema schema, HashSet<string> namespaces, HashSet<IOpenApiSchema> visited)
    {
        if (schema == null || !visited.Add(schema))
            return;

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var externalResource = schemaRef.Reference?.ExternalResource;
            if (!string.IsNullOrEmpty(externalResource))
            {
                var ns = ResolveNamespaceFromRefPath(externalResource);
                if (ns is not null)
                    namespaces.Add(ns);
            }
            return;
        }

        if (schema.AllOf is not null)
        {
            foreach (var item in schema.AllOf)
                CollectExternalNamespacesRecursive(item, namespaces, visited);
        }

        if (schema.OneOf is not null)
        {
            foreach (var item in schema.OneOf)
                CollectExternalNamespacesRecursive(item, namespaces, visited);
        }

        if (schema.AnyOf is not null)
        {
            foreach (var item in schema.AnyOf)
                CollectExternalNamespacesRecursive(item, namespaces, visited);
        }

        if (schema.Properties is not null)
        {
            foreach (var (_, propSchema) in schema.Properties)
                CollectExternalNamespacesRecursive(propSchema, namespaces, visited);
        }

        if (schema.Items is not null)
            CollectExternalNamespacesRecursive(schema.Items, namespaces, visited);
    }

    /// <summary>
    /// Given a relative $ref path (e.g., "../abstract/AbstractContent.1.0.0.json"),
    /// resolves the full path and computes the target namespace based on the
    /// schema definitions directory structure.
    /// </summary>
    private string? ResolveNamespaceFromRefPath(string refPath)
    {
        try
        {
            string currentDir = Path.GetDirectoryName(_context.JsonFilePath) ?? string.Empty;
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, refPath));

            string defsDir = _configuration.Data?.DefinitionsDir ?? _configuration.Api?.DefinitionsDir ?? string.Empty;
            if (string.IsNullOrEmpty(defsDir))
                return null;

            string relativePath = Path.GetRelativePath(defsDir, fullPath);
            string relativeDir = Path.GetDirectoryName(relativePath)?.ToPascalCase() ?? string.Empty;

            return $"{_configuration.Data?.Namespace ?? _configuration.Api?.Namespace}" + (relativeDir == "" ? "" : $".{relativeDir}");
        }
        catch
        {
            return null;
        }
    }
}