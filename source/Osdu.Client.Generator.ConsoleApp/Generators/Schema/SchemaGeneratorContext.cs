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
    /// Cross-file inheritance patches: maps sanitized derived type name to (base class name, base namespace).
    /// NOT cleared on Reset() because these are applied after all files are generated.
    /// </summary>
    public Dictionary<string, (string BaseClassName, string BaseNamespace)> CrossFileInheritancePatches { get; } = new();

    public void Reset()
    {
        GeneratedTypes.Clear();
        PendingBaseClassPatches.Clear();
        OneOfUnionCache.Clear();
    }
}
