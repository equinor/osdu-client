using System.Text;
using Microsoft.Extensions.Logging;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates the IOsduClient interface and OsduClient implementation
/// that aggregates all generated API clients.
/// </summary>
public class OsduClientGenerator
{
    private readonly ILogger<OsduClientGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public OsduClientGenerator(ILogger<OsduClientGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates OsduClient.cs containing both IOsduClient and OsduClient.
    /// </summary>
    /// <param name="apiClientNames">List of PascalCase API client names (e.g., "Dataset", "Search", "Storage").</param>
    public void Generate(IReadOnlyList<string> apiClientNames)
    {
        string outputDir = Directory.GetParent(_configuration.Api.OutputDir)?.FullName!;
        string apiBaseNamespace = _configuration.Api.Namespace;

        Directory.CreateDirectory(outputDir);

        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);

        var orderedNames = apiClientNames.OrderBy(n => n).ToList();

        foreach (string name in orderedNames)
        {
            sb.AppendLine($"using {apiBaseNamespace}.{name};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace Osdu.Client;");
        sb.AppendLine();

        // Interface
        sb.AppendLine("""
            
                      /// <summary>
                      /// Aggregated client interface providing access to all OSDU API clients.
                      /// </summary>
                      public interface IOsduClient
                      {
                    """);

        foreach (string name in orderedNames)
        {
            sb.AppendLine($"    I{name}ApiClient {name} {{ get; }}");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Implementation
        sb.AppendLine("""
            
                      /// <summary>
                      /// Default implementation of <see cref="IOsduClient"/> that aggregates all OSDU API clients.
                      /// </summary>
                      public class OsduClient : IOsduClient
                      {
                    """);

        foreach (string name in orderedNames)
        {
            sb.AppendLine($"    public I{name}ApiClient {name} {{ get; }}");
        }

        sb.AppendLine();
        sb.Append("    public OsduClient(");
        sb.Append(string.Join(", ", orderedNames.Select(n => $"I{n}ApiClient {char.ToLowerInvariant(n[0])}{n[1..]}")));
        sb.AppendLine(")");
        sb.AppendLine("    {");

        foreach (string name in orderedNames)
        {
            string paramName = $"{char.ToLowerInvariant(name[0])}{name[1..]}";
            sb.AppendLine($"        {name} = {paramName};");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        string outputFile = Path.Combine(outputDir, "OsduClient.cs");
        File.WriteAllText(outputFile, sb.ToString());

        _logger.LogInformation($"Generated OsduClient with {apiClientNames.Count} API client(s): {outputFile}");
    }
}
