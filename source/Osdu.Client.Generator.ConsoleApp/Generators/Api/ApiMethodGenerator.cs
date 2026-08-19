using System.Text;
using Microsoft.OpenApi;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates individual API method signatures and implementations.
/// </summary>
public class ApiMethodGenerator
{
    private readonly ApiParameterResolver _parameterResolver;

    public ApiMethodGenerator(ApiParameterResolver parameterResolver)
    {
        _parameterResolver = parameterResolver;
    }

    public void BuildMethodSignature(StringBuilder sb, string path, HttpMethod method, OpenApiOperation operation, bool isInterface, IOpenApiPathItem? pathItem = null)
    {
        string methodName = ApiNamingHelpers.GenerateMethodName(method.Method, path);
        var (returnType, parameters) = _parameterResolver.ResolveMethodDetails(operation, pathItem);
        string paramList = _parameterResolver.BuildParameterList(parameters);

        if (operation.Summary is not null)
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {ApiNamingHelpers.EscapeXml(operation.Summary)}");
            sb.AppendLine($"    /// </summary>");
        }

        string suffix = isInterface ? ";" : "";
        sb.AppendLine($"    {(isInterface ? "" : "public async ")}{(isInterface ? "Task" : "async Task")}<{returnType}> {methodName}Async({paramList}){suffix}");

        if (isInterface)
            sb.AppendLine();
    }

    public void BuildMethod(StringBuilder sb, string path, HttpMethod method, OpenApiOperation operation, IOpenApiPathItem? pathItem = null)
    {
        string methodName = ApiNamingHelpers.GenerateMethodName(method.Method, path);
        var (returnType, parameters) = _parameterResolver.ResolveMethodDetails(operation, pathItem);
        string paramList = _parameterResolver.BuildParameterList(parameters);

        if (operation.Summary is not null)
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {ApiNamingHelpers.EscapeXml(operation.Summary)}");
            sb.AppendLine($"    /// </summary>");
        }

        sb.AppendLine($"    public async Task<{returnType}> {methodName}Async({paramList})");
        sb.AppendLine("    {");

        // Build URL with path and query parameters
        IList<ParameterInfo> pathParams = parameters.Where(p => p.Location == "path").ToList();
        IList<ParameterInfo> queryParams = parameters.Where(p => p.Location == "query").ToList();
        IList<ParameterInfo> headerParams = parameters.Where(p => p.Location == "header").ToList();
        ParameterInfo? bodyParam = parameters.FirstOrDefault(p => p.Location == "body");

        string urlExpr = path;
        foreach (ParameterInfo pathParam in pathParams)
        {
            urlExpr = urlExpr.Replace($"{{{pathParam.OriginalName}}}", $"\x00{pathParam.CSharpName}\x01");
        }
        // Escape remaining braces that are not path parameters (literal path segments)
        urlExpr = urlExpr.Replace("{", "").Replace("}", "");
        // Restore path parameter interpolation braces
        urlExpr = urlExpr.Replace('\x00', '{').Replace('\x01', '}');

        if (queryParams.Any())
        {
            sb.AppendLine($"        var queryParts = new List<string>();");
            foreach (ParameterInfo queryParam in queryParams)
            {
                if (queryParam.Type == "bool?")
                {
                    sb.AppendLine($"        if ({queryParam.CSharpName}.HasValue)");
                    sb.AppendLine($"            queryParts.Add($\"{queryParam.OriginalName}={{{queryParam.CSharpName}.Value.ToString().ToLowerInvariant()}}\");");
                }
                else if (queryParam.Type.EndsWith("?"))
                {
                    sb.AppendLine($"        if ({queryParam.CSharpName} is not null)");
                    sb.AppendLine($"            queryParts.Add($\"{queryParam.OriginalName}={{Uri.EscapeDataString({queryParam.CSharpName}.ToString()!)}}\");");
                }
                else if (!queryParam.IsRequired)
                {
                    sb.AppendLine($"        if ({queryParam.CSharpName} is not null)");
                    sb.AppendLine($"            queryParts.Add($\"{queryParam.OriginalName}={{Uri.EscapeDataString({queryParam.CSharpName}.ToString()!)}}\");");
                }
                else
                {
                    sb.AppendLine($"        queryParts.Add($\"{queryParam.OriginalName}={{Uri.EscapeDataString({queryParam.CSharpName}.ToString()!)}}\");");
                }
            }

            sb.AppendLine($"        var queryString = queryParts.Count > 0 ? \"?\" + string.Join(\"&\", queryParts) : \"\";");
            sb.AppendLine($"        var requestUrl = $\"{{_baseUrl}}{urlExpr}{{queryString}}\";");
        }
        else
        {
            sb.AppendLine($"        var requestUrl = $\"{{_baseUrl}}{urlExpr}\";");
        }

        sb.AppendLine();
        sb.AppendLine($"        using var request = new HttpRequestMessage(HttpMethod.{method.Method.ToLowerInvariant().ToPascalCase()}, requestUrl);");

        // Headers
        foreach (ParameterInfo headerParam in headerParams)
        {
            if (headerParam.IsRequired)
            {
                sb.AppendLine($"        request.Headers.Add(\"{headerParam.OriginalName}\", {headerParam.CSharpName});");
            }
            else
            {
                sb.AppendLine($"        if ({headerParam.CSharpName} is not null)");
                sb.AppendLine($"            request.Headers.Add(\"{headerParam.OriginalName}\", {headerParam.CSharpName});");
            }
        }

        // Body
        if (bodyParam is not null)
        {
            sb.AppendLine($"        request.Content = JsonContent.Create({bodyParam.CSharpName}, options: _jsonOptions);");
        }

        sb.AppendLine("""
                      
                              using var response = await _httpClient.SendAsync(request, cancellationToken);
                              if (!response.IsSuccessStatusCode)
                              {
                                  string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                                  throw new OsduApiException(response.StatusCode, errorBody, requestUrl);
                              }
                      """);


        if (returnType == "string")
        {
            sb.AppendLine("        return await response.Content.ReadAsStringAsync(cancellationToken);");
        }
        else
        {
            sb.AppendLine($"        return await response.Content.ReadFromJsonAsync<{returnType}>(_jsonOptions, cancellationToken)");
            sb.AppendLine($"            ?? throw new InvalidOperationException(\"Response deserialization returned null.\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
