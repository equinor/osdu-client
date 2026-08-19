using System.IO;
using System.Reflection;
using System.Text;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Base class for examples that automatically extracts the RunAsync source code
/// from the implementing class's embedded source file at runtime,
/// and discovers user-configurable parameters via <see cref="ExampleParameterAttribute"/>.
/// </summary>
public abstract class ExampleBase : IExample
{
    private string? _cachedSourceCode;
    private string? _cachedFullSourceCode;
    private IReadOnlyList<ExampleParameterInfo>? _cachedParameters;

    public abstract string Text { get; }

    public abstract string ShortDescription { get; }

    public virtual string Category => "General";

    public string SourceCode => _cachedSourceCode ??= ExtractRunAsyncBody();

    public string FullSourceCode => _cachedFullSourceCode ??= ExtractFullSource();

    public IReadOnlyList<ExampleParameterInfo> Parameters => _cachedParameters ??= DiscoverParameters();

    public abstract Task<string> RunAsync(CancellationToken cancellationToken = default);

    private IReadOnlyList<ExampleParameterInfo> DiscoverParameters()
    {
        return GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<ExampleParameterAttribute>()))
            .Where(x => x.Attribute is not null)
            .OrderBy(x => x.Attribute!.Order)
            .Select(x => new ExampleParameterInfo
            {
                DisplayName = x.Attribute!.DisplayName,
                Description = x.Attribute.Description,
                Required = x.Attribute.Required,
                Order = x.Attribute.Order,
                Property = x.Property,
                PropertyType = x.Property.PropertyType
            })
            .ToList();
    }

    private string ExtractFullSource()
    {
        string exampleFile = GetExampleFile();

        if (!File.Exists(exampleFile))
            return $"// Example file not found: '{exampleFile}'";

        return File.ReadAllText(exampleFile);
    }

    private string ExtractRunAsyncBody()
    {
        string exampleFile = GetExampleFile();

        if (!File.Exists(exampleFile))
            return $"// Example file not found: '{exampleFile}'";

        var lines = File.ReadAllLines(exampleFile);

        // Find the RunAsync method signature
        int methodStart = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("RunAsync") && lines[i].Contains("Task<string>"))
            {
                methodStart = i;
                break;
            }
        }

        if (methodStart < 0)
            return "// RunAsync method not found in source.";

        // Find the opening brace of the method body
        int braceStart = -1;
        for (int i = methodStart; i < lines.Length; i++)
        {
            if (lines[i].Contains('{'))
            {
                braceStart = i;
                break;
            }
        }

        if (braceStart < 0)
            return "// Method body not found.";

        // Track brace depth to find the matching closing brace
        int depth = 0;
        int braceEnd = -1;
        for (int i = braceStart; i < lines.Length; i++)
        {
            foreach (char c in lines[i])
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }

            if (depth == 0)
            {
                braceEnd = i;
                break;
            }
        }

        if (braceEnd < 0)
            return "// Could not determine method boundaries.";

        // Extract lines between the braces and dedent
        var bodyLines = lines[(braceStart + 1)..braceEnd];

        int minIndent = bodyLines
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        var sb = new StringBuilder();
        foreach (var line in bodyLines)
        {
            if (line.Trim().Length == 0)
                sb.AppendLine();
            else
                sb.AppendLine(line.Length >= minIndent ? line[minIndent..] : line);
        }

        return sb.ToString().Trim();
    }

    private string GetExampleFile()
    {
        string exampleFile = $@"{AppDomain.CurrentDomain.BaseDirectory}Examples\{Category}\{GetType().Name}.cs";

        return exampleFile;
    }
}
