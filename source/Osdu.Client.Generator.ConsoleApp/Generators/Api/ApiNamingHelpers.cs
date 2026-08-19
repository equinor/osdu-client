using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Naming and sanitization helpers for API client generation.
/// </summary>
public static class ApiNamingHelpers
{
    public static string SanitizePath(string path) =>
        string.Concat(path.Split(['/', '{', '}', '_', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

    /// <summary>
    /// Generates a clean method name from the HTTP method and API path.
    /// Example: GET /api/rafs-ddms/v2/samplesanalysesreport/{record_id}/versions/{version}
    ///       -> GetSamplesanalysesreportVersionsByRecordIdAndVersion
    /// Example: POST /legaltags:validate -> PostLegaltagsValidate
    /// Example: GET /readiness_check -> GetReadinessCheck
    /// </summary>
    public static string GenerateMethodName(string httpMethod, string path)
    {
        string prefix = httpMethod.ToUpperInvariant() switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            _ => httpMethod.ToPascalCase()
        };

        // Split on both '/' and ':' so that paths like /legaltags:validate
        // produce separate meaningful segments ["legaltags", "validate"]
        var segments = path.Split(['/', ':'], StringSplitOptions.RemoveEmptyEntries).ToList();

        var meaningful = new List<string>();
        var pathParams = new List<string>();

        // Determine where the meaningful segments start by skipping the API prefix.
        // Pattern: "api" / {service-name} / {version} / ...
        // If the path doesn't start with "api", treat all segments as meaningful.
        int startIndex = 0;
        if (segments.Count > 0 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1; // skip "api"
            if (startIndex < segments.Count && !IsVersionSegment(segments[startIndex])
                && !segments[startIndex].StartsWith('{'))
            {
                startIndex++; // skip service name (e.g., "rafs-ddms", "legal")
            }
            if (startIndex < segments.Count && IsVersionSegment(segments[startIndex]))
            {
                startIndex++; // skip version (e.g., "v1", "v2", "dev")
            }
        }

        for (int i = startIndex; i < segments.Count; i++)
        {
            var seg = segments[i];

            if (seg.StartsWith('{') && seg.EndsWith('}'))
            {
                var paramName = seg[1..^1];
                pathParams.Add(paramName.ToPascalCase());
            }
            else
            {
                meaningful.Add(seg.ToPascalCase());
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(prefix);

        foreach (var segment in meaningful)
        {
            sb.Append(segment);
        }

        if (pathParams.Count > 0)
        {
            sb.Append("By");
            sb.Append(string.Join("And", pathParams));
        }

        return sb.ToString();
    }

    private static bool IsVersionSegment(string segment)
    {
        if (segment.Equals("dev", StringComparison.OrdinalIgnoreCase))
            return true;

        if (segment.Length >= 2 && (segment[0] == 'v' || segment[0] == 'V') &&
            segment[1..].All(char.IsDigit))
            return true;

        return false;
    }

    public static string SanitizeParamName(string name)
    {
        var pascal = name.ToPascalCase();
        if (pascal.Length > 0)
            pascal = char.ToLowerInvariant(pascal[0]) + pascal[1..];
        // Avoid C# keywords
        return pascal switch
        {
            "string" or "object" or "default" or "class" or "new" => $"@{pascal}",
            _ => pascal
        };
    }

    public static string EscapeXml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
