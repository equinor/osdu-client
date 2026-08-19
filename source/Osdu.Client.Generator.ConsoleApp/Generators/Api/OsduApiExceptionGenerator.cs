using System.Text;
using Microsoft.Extensions.Logging;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates the OsduApiException class.
/// </summary>
public class OsduApiExceptionGenerator
{
    private readonly ILogger<OsduApiExceptionGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public OsduApiExceptionGenerator(ILogger<OsduApiExceptionGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates OsduApiException.cs in the API output directory.
    /// </summary>
    public void Generate()
    {
        string outputDir = _configuration.Api.OutputDir;
        string apiBaseNamespace = _configuration.Api.Namespace;

        Directory.CreateDirectory(outputDir);

        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);

        sb.AppendLine("using System.Net;");
        sb.AppendLine();
        sb.AppendLine($"namespace {apiBaseNamespace};");
        sb.AppendLine();
        sb.AppendLine("""
                      /// <summary>
                      /// Exception thrown when an OSDU API request fails with a non-success status code.
                      /// </summary>
                      public class OsduApiException : HttpRequestException
                      {
                          /// <summary>
                          /// Gets the response body returned by the API.
                          /// </summary>
                          public string ResponseBody { get; }

                          /// <summary>
                          /// Gets the request URL that caused the exception.
                          /// </summary>
                          public string RequestUrl { get; }

                          public OsduApiException(HttpStatusCode statusCode, string responseBody, string requestUrl)
                              : base($"HTTP {(int)statusCode} from {requestUrl}: {responseBody}", null, statusCode)
                          {
                              ResponseBody = responseBody;
                              RequestUrl = requestUrl;
                          }
                      }
                      """);

        string outputFile = Path.Combine(outputDir, "OsduApiException.cs");
        File.WriteAllText(outputFile, sb.ToString());
        _logger.LogInformation($"Generated OsduApiException: {outputFile}");
    }
}
