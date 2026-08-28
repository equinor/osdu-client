using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Holds shared state for a single schema generation run.
/// </summary>
public class SchemaGeneratorContext
{
    public OpenApiDocument Document { get; set; } = null!;
    public string Namespace { get; set; } = string.Empty;
    public string JsonFilePath { get; set; } = string.Empty;
    public Dictionary<string, string> GeneratedTypes { get; } = new();
    public Dictionary<string, string> PendingBaseClassPatches { get; } = new();
    public Dictionary<string, string> OneOfUnionCache { get; } = new();

    /// <summary>
    /// The top-level schema name for the current file being generated.
    /// All inline type names are anchored to this root to keep them short.
    /// </summary>
    public string RootSchemaName { get; set; } = string.Empty;

    /// <summary>
    /// Cross-file inheritance patches: maps sanitized derived type name to (base class name, base namespace).
    /// NOT cleared on Reset() because these are applied after all files are generated.
    /// </summary>
    public Dictionary<string, (string BaseClassName, string BaseNamespace)> CrossFileInheritancePatches { get; } = new();

    /// <summary>
    /// Registry of inline type names used in the current file.
    /// Maps candidate name to the (parentName, propertyName) key that claimed it.
    /// </summary>
    private readonly Dictionary<string, string> _inlineNameRegistry = new(StringComparer.Ordinal);

    /// <summary>
    /// Cache: (parentName|propertyName) -> resolved short name.
    /// Ensures consistent names across ResolveTypeName and GenerateInlineObjects.
    /// </summary>
    private readonly Dictionary<string, string> _inlineNameCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Generates a short inline type name anchored to the root schema name.
    /// Tries {root}_{prop} first. On conflict, tries {root}_{parentSegment}{prop}.
    /// Falls back to a numeric suffix if still conflicting.
    /// </summary>
    public string GetShortInlineName(string parentName, string pascalPropertyName)
    {
        var cacheKey = $"{parentName}|{pascalPropertyName}";
        if (_inlineNameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var root = RootSchemaName;

        // Strategy 1: root + property name (shortest)
        var candidate = $"{root}_{pascalPropertyName}";
        if (TryClaimName(candidate, cacheKey))
            return candidate;

        // Strategy 2: root + last parent segment + property name
        var parentSegment = GetLastSegment(parentName, root);
        if (!string.IsNullOrEmpty(parentSegment))
        {
            candidate = $"{root}_{parentSegment}{pascalPropertyName}";
            if (TryClaimName(candidate, cacheKey))
                return candidate;
        }

        // Strategy 3: numeric suffix on the short form
        candidate = $"{root}_{pascalPropertyName}";
        int counter = 2;
        while (true)
        {
            var numbered = $"{candidate}{counter}";
            if (TryClaimName(numbered, cacheKey))
                return numbered;
            counter++;
        }
    }

    /// <summary>
    /// Generates a short variant type name for discriminated unions.
    /// Uses {root}_{variantName} instead of {fullUnionBase}{variantName}.
    /// </summary>
    public string GetShortVariantName(string unionBaseName, string variantName)
    {
        // Reuse the same infrastructure — treat it as parent=unionBase, prop=variantName
        return GetShortInlineName(unionBaseName, variantName);
    }

    private bool TryClaimName(string candidate, string cacheKey)
    {
        if (_inlineNameRegistry.TryGetValue(candidate, out var existingKey))
        {
            if (existingKey == cacheKey)
            {
                _inlineNameCache[cacheKey] = candidate;
                return true;
            }
            return false; // conflict
        }

        _inlineNameRegistry[candidate] = cacheKey;
        _inlineNameCache[cacheKey] = candidate;
        return true;
    }

    /// <summary>
    /// Extracts the last meaningful segment from a parent name, excluding the root prefix.
    /// E.g., for parent "Root_Features_Geometry" and root "Root", returns "Geometry".
    /// </summary>
    private static string GetLastSegment(string parentName, string root)
    {
        if (parentName == root)
            return string.Empty;

        var lastUnderscore = parentName.LastIndexOf('_');
        if (lastUnderscore < 0)
            return parentName;

        return parentName[(lastUnderscore + 1)..];
    }

    public void Reset()
    {
        GeneratedTypes.Clear();
        PendingBaseClassPatches.Clear();
        OneOfUnionCache.Clear();
        _inlineNameRegistry.Clear();
        _inlineNameCache.Clear();
        RootSchemaName = string.Empty;
    }
}