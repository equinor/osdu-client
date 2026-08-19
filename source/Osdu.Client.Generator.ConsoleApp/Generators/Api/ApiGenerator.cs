using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

public class ApiGenerator
{
    private readonly ILogger<ApiGenerator> _logger;
    private readonly AppConfiguration _configuration;

    private readonly ApiTypeResolver _typeResolver;
    private readonly ApiParameterResolver _parameterResolver;
    private readonly ApiMethodGenerator _methodGenerator;
    private readonly ApiClassBuilder _classBuilder;

    public ApiGenerator(ILogger<ApiGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _typeResolver = new ApiTypeResolver();
        _parameterResolver = new ApiParameterResolver(_typeResolver);
        _methodGenerator = new ApiMethodGenerator(_parameterResolver);
        _classBuilder = new ApiClassBuilder(_methodGenerator);
    }

    public void Generate(string jsonFile, string outputDir, string baseNamespace)
    {
        string apiClientName = Path.GetFileNameWithoutExtension(jsonFile).ToPascalCase();
        string apiNamespace = $"{baseNamespace}.{apiClientName}";

        string jsonContent = File.ReadAllText(jsonFile);

        ReadResult? result = OpenApiDocument.Parse(jsonContent, "json");
        OpenApiDocument? openApiDocument = result?.Document;

        if (openApiDocument == null)
        {
            _logger.LogError($"  Failed to parse OpenAPI document from definition file: {jsonFile}");
            return;
        }

        StringBuilder sb = new StringBuilder();

        CodeGenerator.BuildAutogenComment(sb);
        _classBuilder.BuildUsingsAndNamespace(sb, apiNamespace);
        _classBuilder.BuildInterface(sb, openApiDocument, $"{apiClientName}");
        _classBuilder.BuildImplementation(sb, openApiDocument, apiClientName);

        string outputFile = Path.Combine(outputDir, apiClientName, $"{apiClientName}ApiClient.cs");

        if (!Directory.Exists(Path.GetDirectoryName(outputFile)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
        }

        _logger.LogInformation($"    Generated API client: {apiClientName}ApiClient.cs");
        File.WriteAllText(outputFile, sb.ToString());
    }
}
