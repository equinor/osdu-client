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
    /// Finds polymorphic base classes that use [JsonPolymorphic(TypeDiscriminatorPropertyName = "X")]
    /// and adds [JsonIgnore] to any property in their derived classes whose [JsonPropertyName] matches
    /// the discriminator property name. This prevents System.Text.Json from throwing
    /// InvalidOperationException at runtime.
    /// </summary>
    private void FixDiscriminatorPropertyConflicts()
    {
        // Pattern to find: [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
        var polymorphicPattern = new Regex(
            @"\[JsonPolymorphic\(TypeDiscriminatorPropertyName\s*=\s*""(?<disc>[^""]+)""\)\]");

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

            // Check if any class in this file is a known derived type
            foreach (var (derivedTypeName, discriminatorName) in discriminatorsByDerivedType)
            {
                if (!code.Contains($"class {derivedTypeName}"))
                    continue;

                // Only apply [JsonIgnore] if this class actually inherits from a polymorphic base
                // (i.e., the class declaration contains " : SomeBaseClass")
                var classPattern = new Regex($@"class\s+{Regex.Escape(derivedTypeName)}\s*:\s*\w+");
                if (!classPattern.IsMatch(code))
                    continue;

                // Find the [JsonPropertyName("type")] line that matches the discriminator
                // and insert [JsonIgnore] before it if not already present
                string propertyNameAttr = $"[JsonPropertyName(\"{discriminatorName}\")]";
                if (!code.Contains(propertyNameAttr))
                    continue;

                // Add [JsonIgnore] before the [JsonPropertyName("...")] for the discriminator property
                // and remove [Required] attribute and 'required' modifier since JsonIgnore prevents deserialization
                var jsonIgnorePattern = new Regex(
                    @"(?<indent>[ \t]*)\[Required\]\s*\n(?<indent2>[ \t]*)" +
                    Regex.Escape(propertyNameAttr));

                code = jsonIgnorePattern.Replace(code, match =>
                {
                    // Only add if [JsonIgnore] is not already there
                    if (code.LastIndexOf("[JsonIgnore]", match.Index, StringComparison.Ordinal) >= 0)
                    {
                        int checkStart = Math.Max(0, match.Index - 50);
                        string preceding = code[checkStart..match.Index];
                        if (preceding.Contains("[JsonIgnore]"))
                            return match.Value;
                    }

                    string indent = match.Groups["indent"].Value;
                    string indent2 = match.Groups["indent2"].Value;
                    return $"{indent}[JsonIgnore]\n{indent2}{propertyNameAttr}";
                });

                // Remove 'required' modifier from the discriminator property line
                var requiredModifierPattern = new Regex(
                    @"(public\s+)required(\s+\S+\s+" + Regex.Escape(SchemaHelpers.Sanitize(discriminatorName.ToPascalCase())) + @"\s*\{)");
                code = requiredModifierPattern.Replace(code, "$1$2");

                _context.GeneratedTypes[name] = code;
            }
        }
    }


    private string AddOpenApiHeader(string jsonContent, string schemaName)
    {
        // Wrap the JSON schema in a minimal OpenAPI document so we can reuse SchemaGenerator
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
            string fullRefPath = Path.GetFullPath(Path.Combine(currentDir, refPath));
            string definitionsDir = _configuration.Data.DefinitionsDir;

            string relativePath = Path.GetRelativePath(definitionsDir, fullRefPath);
            string relativeDir = Path.GetDirectoryName(relativePath)?.ToPascalCase() ?? string.Empty;

            if (string.IsNullOrEmpty(relativeDir))
                return _configuration.Data.Namespace;

            return $"{_configuration.Data.Namespace}.{relativeDir}";
        }
        catch
        {
            return null;
        }
    }
}
