using System.Text.Json;

namespace Osdu.Client.ExampleApp.Helpers;

/// <summary>
/// Utilities for flattening JSON to tabular form and building tree nodes.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Extracts column names from a list of JSON records (union of all keys).
    /// </summary>
    public static List<string> ExtractColumns(IReadOnlyList<JsonElement> records)
    {
        var columns = new LinkedHashSet();
        foreach (var record in records)
        {
            if (record.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in record.EnumerateObject())
                columns.Add(prop.Name);
        }
        return columns.ToList();
    }

    /// <summary>
    /// Flattens a JSON record into a dictionary of column → display string.
    /// Arrays show "N items", nested objects show "{...}".
    /// </summary>
    public static Dictionary<string, CellValue> FlattenRecord(JsonElement record)
    {
        var row = new Dictionary<string, CellValue>();
        if (record.ValueKind != JsonValueKind.Object) return row;

        foreach (var prop in record.EnumerateObject())
        {
            row[prop.Name] = ToCellValue(prop.Value);
        }
        return row;
    }

    public static CellValue ToCellValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Array =>
                new CellValue($"{value.GetArrayLength()} items", value, IsExpandable: true),
            JsonValueKind.Object =>
                new CellValue("{...}", value, IsExpandable: true),
            JsonValueKind.String =>
                new CellValue(value.GetString() ?? "", value),
            JsonValueKind.Number =>
                new CellValue(value.GetRawText(), value),
            JsonValueKind.True or JsonValueKind.False =>
                new CellValue(value.GetRawText(), value),
            JsonValueKind.Null or JsonValueKind.Undefined =>
                new CellValue("null", value),
            _ => new CellValue(value.GetRawText(), value)
        };
    }

    /// <summary>
    /// Builds tree nodes from a JsonElement.
    /// </summary>
    public static List<JsonTreeNode> BuildTree(JsonElement element, string name = "root")
    {
        var nodes = new List<JsonTreeNode>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var node = new JsonTreeNode(prop.Name, prop.Value);
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        node.Children = BuildTree(prop.Value, prop.Name);
                    nodes.Add(node);
                }
                break;

            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var node = new JsonTreeNode($"[{i}]", item);
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        node.Children = BuildTree(item, $"[{i}]");
                    nodes.Add(node);
                    i++;
                }
                break;
        }

        return nodes;
    }

    /// <summary>
    /// Maintains insertion order like LinkedHashSet.
    /// </summary>
    private class LinkedHashSet
    {
        private readonly List<string> _list = [];
        private readonly HashSet<string> _set = [];

        public void Add(string item)
        {
            if (_set.Add(item)) _list.Add(item);
        }

        public List<string> ToList() => [.. _list];
    }
}

public record CellValue(string Display, JsonElement Raw, bool IsExpandable = false);

public class JsonTreeNode(string key, JsonElement value)
{
    public string Key { get; } = key;
    public JsonElement Value { get; } = value;
    public string DisplayValue => Value.ValueKind switch
    {
        JsonValueKind.Object => $"{{{Value.EnumerateObject().Count()}}}",
        JsonValueKind.Array => $"[{Value.GetArrayLength()} items]",
        _ => Value.ToString()
    };
    public bool IsLeaf => Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array);
    public List<JsonTreeNode>? Children { get; set; }
    public string CopyText => Value.ValueKind switch
    {
        JsonValueKind.Object or JsonValueKind.Array =>
            JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }),
        _ => Value.ToString()
    };
}