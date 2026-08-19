using System.Text;
using Microsoft.Extensions.Logging;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates the ServicesExtension class that registers all generated API clients with DI.
/// </summary>
public class ServiceCollectionExtensionsGenerator
{
    private readonly ILogger<ServiceCollectionExtensionsGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public ServiceCollectionExtensionsGenerator(ILogger<ServiceCollectionExtensionsGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates the ServicesExtension.cs file based on discovered API definition files.
    /// </summary>
    /// <param name="apiClientNames">List of PascalCase API client names (e.g., "Dataset", "Search", "Storage").</param>
    public void Generate(IReadOnlyList<string> apiClientNames)
    {
        string outputDir = _configuration.Extension.OutputDir;
        string extensionNamespace = _configuration.Extension.Namespace;
        string apiBaseNamespace = _configuration.Api.Namespace;

        Directory.CreateDirectory(outputDir);

        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);
        BuildUsings(sb);

        // Add using for each API client namespace
        foreach (string name in apiClientNames.OrderBy(n => n))
        {
            sb.AppendLine($"using {apiBaseNamespace}.{name};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {extensionNamespace};");
        sb.AppendLine();
        sb.AppendLine("""
                      /// <summary>
                      /// Extension methods for registering OSDU API clients with dependency injection.
                      /// </summary>
                      public static class ServiceCollectionExtensions
                      {
                          /// <summary>
                          /// Registers all generated OSDU API clients with the service collection.
                          /// </summary>
                          /// <param name="services">The service collection.</param>
                          /// <param name="configureHttpClient">Optional configuration for the underlying <see cref="HttpClient"/>.</param>
                          /// <param name="configureHttpClientBuilder">Optional configuration for each <see cref="IHttpClientBuilder"/> (e.g., to add message handlers).</param>
                          /// <returns>The service collection for chaining.</returns>
                          public static IServiceCollection AddOsduApiClients(this IServiceCollection services, Action<HttpClient>? configureHttpClient = null, Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
                          {
                      """);

        foreach (string name in apiClientNames.OrderBy(n => n))
        {
            sb.AppendLine($$"""
                                    IHttpClientBuilder {{char.ToLowerInvariant(name[0])}}{{name[1..]}}Builder = services.AddHttpClient<I{{name}}ApiClient, {{name}}ApiClient>(httpClient => 
                                    { 
                                        configureHttpClient?.Invoke(httpClient); 
                                    });
                                    configureHttpClientBuilder?.Invoke({{char.ToLowerInvariant(name[0])}}{{name[1..]}}Builder);
                                    
                            """);
        }

        // Register the aggregated IOsduClient / OsduClient
        sb.AppendLine();
        sb.AppendLine("        services.AddScoped<IOsduClient, OsduClient>();");
        sb.AppendLine("""
                      
                              return services;
                          }
                      }
                      """);

        string outputFile = Path.Combine(outputDir, "ServiceCollectionExtensions.cs");
        File.WriteAllText(outputFile, sb.ToString());

        _logger.LogInformation($"Generated service collection extensions: {outputFile}");
    }

    public void BuildUsings(StringBuilder sb)
    {
        sb.AppendLine($"""
                       using System;
                       using System.Net.Http;
                       using Microsoft.Extensions.DependencyInjection;
                       using {_configuration.Api.Namespace};

                       """);
    }
}
