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

    /// <summary>
    /// C# built-in value type keywords that can never be null in their non-nullable form.
    /// Used to skip validation for parameters that the compiler already guarantees are non-null.
    /// </summary>
    private static readonly HashSet<string> ValueTypeKeywords =
    [
        "bool", "byte", "sbyte", "char",
        "short", "ushort", "int", "uint",
        "long", "ulong", "nint", "nuint",
        "float", "double", "decimal"
    ];

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

        // Categorize parameters
        IList<ParameterInfo> pathParams = parameters.Where(p => p.Location == "path").ToList();
        IList<ParameterInfo> queryParams = parameters.Where(p => p.Location == "query").ToList();
        IList<ParameterInfo> headerParams = parameters.Where(p => p.Location == "header").ToList();
        ParameterInfo? bodyParam = parameters.FirstOrDefault(p => p.Location == "body");

        // Emit parameter validation
        bool hasValidation = false;

        // Path parameters are always required
        foreach (ParameterInfo pathParam in pathParams)
        {
            hasValidation |= EmitScalarValidation(sb, pathParam, isRequired: true);
        }

        // Required query parameters
        foreach (ParameterInfo queryParam in queryParams)
        {
            hasValidation |= EmitScalarValidation(sb, queryParam, queryParam.IsRequired);
        }

        // Required header parameters
        foreach (ParameterInfo headerParam in headerParams)
        {
            hasValidation |= EmitScalarValidation(sb, headerParam, headerParam.IsRequired);
        }

        // Body parameter
        if (bodyParam is not null)
        {
            hasValidation |= EmitBodyValidation(sb, bodyParam);
        }

        if (hasValidation)
            sb.AppendLine();

        // Build URL with path and query parameters
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

    /// <summary>
    /// Returns true if the type string represents a non-nullable value type
    /// that the compiler guarantees cannot be null.
    /// </summary>
    private static bool IsNonNullableValueType(string type) => ValueTypeKeywords.Contains(type);

    /// <summary>
    /// Returns true if the type string represents a nullable value type
    /// (e.g., "int?", "bool?", "Nullable&lt;int&gt;", "Nullable&lt;long&gt;").
    /// </summary>
    private static bool IsNullableValueType(string type)
    {
        if (type.EndsWith("?"))
            return ValueTypeKeywords.Contains(type[..^1]);

        if (type.StartsWith("Nullable<") && type.EndsWith(">"))
            return ValueTypeKeywords.Contains(type["Nullable<".Length..^1]);

        return false;
    }

    /// <summary>
    /// Returns true if the type has no DataAnnotation attributes worth validating.
    /// Primitives, value types, string, and object have no [Required]/[RegularExpression] etc.
    /// </summary>
    private static bool IsPrimitiveOrValueType(string type) =>
        type is "string" or "object"
        || IsNonNullableValueType(type)
        || IsNullableValueType(type);

    /// <summary>
    /// Emits validation for a scalar (non-body) parameter: path, query, or header.
    /// Returns true if a validation line was emitted.
    /// </summary>
    private static bool EmitScalarValidation(StringBuilder sb, ParameterInfo param, bool isRequired)
    {
        if (!isRequired)
            return false;

        // Non-nullable value types (int, long, bool, etc.) cannot be null — skip
        if (IsNonNullableValueType(param.Type))
            return false;

        if (param.Type == "string")
        {
            sb.AppendLine($"        RequestValidator.RequireNotNullOrEmpty({param.CSharpName}, nameof({param.CSharpName}));");
            return true;
        }

        if (IsNullableValueType(param.Type))
        {
            sb.AppendLine($"        RequestValidator.RequireNotNull({param.CSharpName}, nameof({param.CSharpName}));");
            return true;
        }

        // Any other reference type (DateTimeOffset, custom structs passed as nullable, etc.)
        sb.AppendLine($"        RequestValidator.RequireNotNull({param.CSharpName}, nameof({param.CSharpName}));");
        return true;
    }

    /// <summary>
    /// Emits validation for a body parameter based on its resolved C# type.
    /// Returns true if a validation line was emitted.
    /// </summary>
    private static bool EmitBodyValidation(StringBuilder sb, ParameterInfo bodyParam)
    {
        string type = bodyParam.Type;

        // List<T> body
        if (type.StartsWith("List<") && type.EndsWith(">"))
        {
            string innerType = type["List<".Length..^1];

            if (IsPrimitiveOrValueType(innerType))
            {
                // List<string>, List<int>, List<object>, etc. — just null + empty check
                sb.AppendLine($"        RequestValidator.RequireNotNullOrEmptyList({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            }
            else
            {
                // List<Record>, List<SomeModel>, etc. — validate each item via DataAnnotations
                sb.AppendLine($"        RequestValidator.ValidateObjectList({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            }

            return true;
        }

        // Dictionary<K,V> body — just null check, no DataAnnotations to validate
        if (type.StartsWith("Dictionary<"))
        {
            sb.AppendLine($"        RequestValidator.RequireNotNull({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            return true;
        }

        // string body
        if (type == "string")
        {
            sb.AppendLine($"        RequestValidator.RequireNotNullOrEmpty({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            return true;
        }

        // Non-nullable value type — compiler prevents null, skip
        if (IsNonNullableValueType(type))
            return false;

        // Nullable value type — null check only
        if (IsNullableValueType(type))
        {
            sb.AppendLine($"        RequestValidator.RequireNotNull({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            return true;
        }

        // "object" body — no DataAnnotations possible, null check only
        if (type == "object")
        {
            sb.AppendLine($"        RequestValidator.RequireNotNull({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
            return true;
        }

        // Complex object — full DataAnnotations validation
        sb.AppendLine($"        RequestValidator.ValidateObject({bodyParam.CSharpName}, nameof({bodyParam.CSharpName}));");
        return true;
    }
}
