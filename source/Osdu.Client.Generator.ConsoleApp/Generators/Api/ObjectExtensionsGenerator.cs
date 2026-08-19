using System.Text;
using Microsoft.Extensions.Logging;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates the ObjectExtensions class with helper methods for deserializing untyped results.
/// </summary>
public class ObjectExtensionsGenerator
{
    private readonly ILogger<ObjectExtensionsGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public ObjectExtensionsGenerator(ILogger<ObjectExtensionsGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates the ObjectExtensions.cs file.
    /// </summary>
    public void Generate()
    {
        string outputDir = _configuration.Extension.OutputDir;
        string extensionNamespace = _configuration.Extension.Namespace;

        Directory.CreateDirectory(outputDir);

        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);

        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        sb.AppendLine($"namespace {extensionNamespace};");
        sb.AppendLine();
        sb.AppendLine("""
                      /// <summary>
                      /// Extension methods for deserializing untyped results to strongly-typed objects.
                      /// </summary>
                      public static class ObjectExtensions
                      {
                          /// <summary>
                          /// Deserializes an object (typically a <see cref="JsonElement"/>) to the specified type.
                          /// </summary>
                          public static T Deserialize<T>(this object obj, JsonSerializerOptions? options = null)
                          {
                              return obj switch
                              {
                                  JsonElement element => JsonSerializer.Deserialize<T>(element, options)!,
                                  T typed => typed,
                                  _ => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, options), options)!
                              };
                          }

                          /// <summary>
                          /// Deserializes a list of objects to a strongly-typed list.
                          /// </summary>
                          public static List<T> Deserialize<T>(this IEnumerable<object> objects, JsonSerializerOptions? options = null)
                          {
                              return objects.Select(obj => obj.Deserialize<T>(options)).ToList();
                          }
                      }
                      """);

        string outputPath = Path.Combine(outputDir, "ObjectExtensions.cs");
        File.WriteAllText(outputPath, sb.ToString());

        _logger.LogInformation($"Generated ObjectExtensions: {outputPath}");
    }
}
