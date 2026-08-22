namespace Osdu.Client.ExampleApp.Services;

/// <summary>
/// Represents a single filter condition in the query builder.
/// </summary>
public class FilterCondition
{
    public string PropertyPath { get; set; } = "";
    public string Operator { get; set; } = "equals";
    public string Value { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public PropertyInfo? PropertyInfo { get; set; }

    /// <summary>Converts this condition to a Lucene query fragment.</summary>
    public string ToLucene()
    {
        if (string.IsNullOrWhiteSpace(PropertyPath)) return "";

        var rawQuery = Operator switch
        {
            "equals" => $"{PropertyPath}:\"{EscapeValue(Value)}\"",
            "not equals" => $"(NOT {PropertyPath}:\"{EscapeValue(Value)}\")",
            "contains" => $"{PropertyPath}:*{EscapeValue(Value)}*",
            "does not contain" => $"(NOT {PropertyPath}:*{EscapeValue(Value)}*)",
            "starts with" => $"{PropertyPath}:{EscapeValue(Value)}*",
            "ends with" => $"{PropertyPath}:*{EscapeValue(Value)}",
            "match" => $"{PropertyPath}:{EscapeValue(Value)}",
            "wildcard" => $"{PropertyPath}:{Value}",
            "greater than" => $"{PropertyPath}:{{{EscapeValue(Value)} TO *}}",
            "greater than or equal" => $"{PropertyPath}:[{EscapeValue(Value)} TO *]",
            "less than" => $"{PropertyPath}:{{* TO {EscapeValue(Value)}}}",
            "less than or equal" => $"{PropertyPath}:[* TO {EscapeValue(Value)}]",
            "between" => BuildBetween(),
            "is null" => $"(NOT _exists_:{PropertyPath})",
            "is not null" => $"_exists_:{PropertyPath}",
            _ => $"{PropertyPath}:\"{EscapeValue(Value)}\""
        };

        // Wrap in nested() if the path goes through an array field
        return WrapNestedIfRequired(rawQuery);
    }

    /// <summary>
    /// Detects if the property path traverses an array (nested) field and wraps
    /// the query with OSDU's <c>nested(parentPath, query)</c> syntax.
    /// <para>
    /// Example: <c>data.Curves.CurveID:"MD"</c> becomes
    /// <c>nested(data.Curves, data.Curves.CurveID:"MD")</c>
    /// </para>
    /// </summary>
    private string WrapNestedIfRequired(string query)
    {
        // Walk ancestors from PropertyInfo to find if any parent is an Array type
        if (PropertyInfo is null) return query;

        // Find the nearest array ancestor in the path
        var nestedPath = FindNestedArrayPath(PropertyPath);
        if (nestedPath is null) return query;

        return $"nested({nestedPath}, {query})";
    }

    /// <summary>
    /// Finds the nested array parent path by checking the PropertyInfo hierarchy.
    /// For example, for path "data.Curves.CurveID", if "data.Curves" is an Array,
    /// returns "data.Curves".
    /// </summary>
    private string? FindNestedArrayPath(string fullPath)
    {
        if (PropertyInfo?.ParentInfo is null) return null;

        // Walk up the parent chain to find the nearest array ancestor
        var current = PropertyInfo.ParentInfo;
        while (current is not null)
        {
            if (current.Kind == PropertyKind.Array)
                return current.Path;
            current = current.ParentInfo;
        }

        return null;
    }

    /// <summary>Escapes special Lucene characters in the value.</summary>
    private static string EscapeValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var sb = new System.Text.StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (c is '+' or '-' or '!' or '(' or ')' or '{' or '}' or '[' or ']'
                     or '^' or '~' or '\\' or '/')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private string BuildBetween()
    {
        var parts = Value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
            return $"{PropertyPath}:[{EscapeValue(parts[0])} TO {EscapeValue(parts[1])}]";
        return $"{PropertyPath}:\"{EscapeValue(Value)}\"";
    }
}

/// <summary>
/// Metadata about a property discovered from a kind's corresponding type.
/// </summary>
public class PropertyInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string JsonName { get; set; } = "";
    public PropertyKind Kind { get; set; } = PropertyKind.String;
    public List<PropertyInfo> Children { get; set; } = [];

    /// <summary>
    /// Reference to the parent property, used to detect nested array ancestors
    /// for generating <c>nested()</c> query wrappers.
    /// </summary>
    public PropertyInfo? ParentInfo { get; set; }

    public string DisplayName => string.IsNullOrEmpty(JsonName) ? Name : JsonName;
}

public enum PropertyKind
{
    String,
    Number,
    Boolean,
    DateTime,
    Object,
    Array
}