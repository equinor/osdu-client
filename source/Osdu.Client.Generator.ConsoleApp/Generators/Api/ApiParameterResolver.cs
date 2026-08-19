using Microsoft.OpenApi;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Resolves method parameters and return types from OpenAPI operations.
/// </summary>
public class ApiParameterResolver
{
    private readonly ApiTypeResolver _typeResolver;

    public ApiParameterResolver(ApiTypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
    }

    public (string ReturnType, IList<ParameterInfo> Parameters) ResolveMethodDetails(OpenApiOperation operation, IOpenApiPathItem? pathItem = null)
    {
        IList<ParameterInfo> parameters = new List<ParameterInfo>();

        // Merge path-level parameters with operation-level parameters.
        // Operation parameters override path-level parameters with the same name and location.
        var allParams = new List<IOpenApiParameter>();

        if (pathItem?.Parameters is not null)
        {
            allParams.AddRange(pathItem.Parameters);
        }

        if (operation.Parameters is not null)
        {
            foreach (var opParam in operation.Parameters)
            {
                // Remove any path-level param with the same name/location (operation overrides)
                allParams.RemoveAll(p => p.Name == opParam.Name && p.In == opParam.In);
                allParams.Add(opParam);
            }
        }

        // Path and query parameters only (headers are handled by DelegatingHandler)
        foreach (var param in allParams)
        {
            if (param.In.ToString()!.Equals("header", StringComparison.OrdinalIgnoreCase))
                continue;

            string csharpType = _typeResolver.ResolveParamType(param.Schema);
            bool isRequired = param.Required;
            if (!isRequired && !csharpType.EndsWith("?") && csharpType != "string")
                csharpType += "?";

            parameters.Add(new ParameterInfo
            {
                OriginalName = param.Name!,
                CSharpName = ApiNamingHelpers.SanitizeParamName(param.Name!),
                Type = csharpType,
                Location = param.In.ToString()!.ToLowerInvariant(),
                IsRequired = isRequired
            });
        }

        // Request body
        if (operation.RequestBody?.Content is not null)
        {
            foreach (var (_, mediaType) in operation.RequestBody.Content)
            {
                string typeName = _typeResolver.ResolveSchemaTypeName(mediaType.Schema);
                parameters.Add(new ParameterInfo
                {
                    OriginalName = "body",
                    CSharpName = "body",
                    Type = typeName,
                    Location = "body",
                    IsRequired = operation.RequestBody.Required
                });
                break; // use first content type
            }
        }

        // CancellationToken always last
        parameters.Add(new ParameterInfo
        {
            OriginalName = "cancellationToken",
            CSharpName = "cancellationToken",
            Type = "CancellationToken",
            Location = "special",
            IsRequired = false,
            DefaultValue = "default"
        });

        // Resolve return type from success response
        string returnType = "string";
        var successEntry = operation.Responses
            .Where(r => r.Key.StartsWith("2"))
            .OrderBy(r => r.Key)
            .FirstOrDefault(r => r.Value.Content is not null && r.Value.Content.Count > 0);

        if (successEntry.Value?.Content is not null)
        {
            foreach (var (contentType, mediaType) in successEntry.Value.Content)
            {
                if (contentType.Contains("json"))
                {
                    returnType = _typeResolver.ResolveSchemaTypeName(mediaType.Schema);
                }
                else if (contentType.Contains("text"))
                {
                    returnType = "string";
                }

                break;
            }
        }

        // Reorder: required first, optional after, cancellationToken last
        IList<ParameterInfo> ordered = parameters
            .Where(p => p.Location != "special" && p.IsRequired)
            .Concat(parameters.Where(p => p.Location != "special" && !p.IsRequired))
            .Concat(parameters.Where(p => p.Location == "special"))
            .ToList();

        return (returnType, ordered);
    }

    public string BuildParameterList(IList<ParameterInfo> parameters)
    {
        IList<string> parts = new List<string>();
        foreach (ParameterInfo param in parameters)
        {
            var defaultVal = param.DefaultValue is not null ? $" = {param.DefaultValue}" : "";
            if (!param.IsRequired && param.Location != "special" && param.DefaultValue is null)
                defaultVal = " = default";
            parts.Add($"{param.Type} {param.CSharpName}{defaultVal}");
        }

        return string.Join(", ", parts);
    }
}
