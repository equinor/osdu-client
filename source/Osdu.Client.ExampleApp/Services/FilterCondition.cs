namespace Osdu.Client.ExampleApp.Services;

/// <summary>
/// Represents a single filter condition in the query builder.
/// </summary>
public class FilterCondition
{
    public string PropertyPath { get; set; } = "";
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public PropertyInfo? PropertyInfo { get; set; }

    /// <summary>Converts this condition to a Lucene query fragment.</summary>
    public string ToLucene()
    {
        if (string.IsNullOrWhiteSpace(PropertyPath)) return "";

        return Operator switch
        {
            "equals" => $"{PropertyPath}:\"{Value}\"",
            "not equals" => $"NOT {PropertyPath}:\"{Value}\"",
            "contains" => $"{PropertyPath}:*{Value}*",
            "does not contain" => $"NOT {PropertyPath}:*{Value}*",
            "starts with" => $"{PropertyPath}:{Value}*",
            "ends with" => $"{PropertyPath}:*{Value}",
            "greater than" => $"{PropertyPath}:{{{Value} TO *}}",
            "greater than or equal" => $"{PropertyPath}:[{Value} TO *]",
            "less than" => $"{PropertyPath}:{{* TO {Value}}}",
            "less than or equal" => $"{PropertyPath}:[* TO {Value}]",
            "is null" => $"NOT _exists_:{PropertyPath}",
            "is not null" => $"_exists_:{PropertyPath}",
            "between" => BuildBetween(),
            _ => $"{PropertyPath}:\"{Value}\""
        };
    }

    private string BuildBetween()
    {
        var parts = Value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
            return $"{PropertyPath}:[{parts[0]} TO {parts[1]}]";
        return $"{PropertyPath}:\"{Value}\"";
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