using System.Reflection;
using System.Text.Json.Serialization;

namespace Osdu.Client.ExampleApp.Services;

/// <summary>
/// Resolves properties for a given OSDU kind by finding the corresponding
/// C# type in the Osdu.Client.Data namespace.
/// </summary>
public static class KindPropertyResolver
{
    private static readonly Dictionary<string, List<PropertyInfo>> s_cache = [];

    /// <summary>
    /// Gets properties for a kind ID like "osdu:wks:master-data--Well:1.0.0"
    /// by finding the corresponding type in the Osdu.Client.Data namespace.
    /// </summary>
    public static List<PropertyInfo> GetProperties(string kindId)
    {
        if (s_cache.TryGetValue(kindId, out var cached))
            return cached;

        var type = ResolveType(kindId);
        if (type is null)
        {
            // Fallback: return common OSDU properties
            var fallback = GetCommonOsduProperties();
            s_cache[kindId] = fallback;
            return fallback;
        }

        var props = ExtractProperties(type, "", maxDepth: 4);
        s_cache[kindId] = props;
        return props;
    }

    private static Type? ResolveType(string kindId)
    {
        // Kind format: authority:source:entityType:major.minor.patch
        var parts = kindId.Split(':');
        if (parts.Length < 4) return null;

        var entityType = parts[2]; // e.g. "master-data--Well"
        var version = parts[3];    // e.g. "1.0.0"

        // Convert to class name: Well_1_0_0
        var typeName = entityType.Contains("--")
            ? entityType[(entityType.LastIndexOf("--") + 2)..]
            : entityType;

        var versionSuffix = version.Replace('.', '_');
        var className = $"{typeName}_{versionSuffix}";

        // Search in Osdu.Client assembly
        var assembly = typeof(Osdu.Client.OsduClient).Assembly;
        var matchedType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name.Equals(className, StringComparison.OrdinalIgnoreCase)
                              && t.Namespace?.StartsWith("Osdu.Client.Data") == true);

        return matchedType;
    }

    private static List<PropertyInfo> ExtractProperties(Type type, string parentPath, int maxDepth)
    {
        if (maxDepth <= 0) return [];

        var result = new List<PropertyInfo>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var jsonName = jsonAttr?.Name ?? prop.Name;
            var fullPath = string.IsNullOrEmpty(parentPath) ? jsonName : $"{parentPath}.{jsonName}";

            var propInfo = new PropertyInfo
            {
                Name = prop.Name,
                JsonName = jsonName,
                Path = fullPath,
                Kind = ResolvePropertyKind(prop.PropertyType)
            };

            // If it's an object type, recursively get children
            if (propInfo.Kind == PropertyKind.Object)
            {
                var innerType = GetInnerType(prop.PropertyType);
                if (innerType is not null && !innerType.Namespace?.StartsWith("System") == true)
                {
                    propInfo.Children = ExtractProperties(innerType, fullPath, maxDepth - 1);
                }
            }
            else if (propInfo.Kind == PropertyKind.Array)
            {
                var elementType = GetCollectionElementType(prop.PropertyType);
                if (elementType is not null && !elementType.Namespace?.StartsWith("System") == true)
                {
                    propInfo.Children = ExtractProperties(elementType, fullPath, maxDepth - 1);
                }
            }

            result.Add(propInfo);
        }

        return result.OrderBy(p => p.JsonName).ToList();
    }

    private static PropertyKind ResolvePropertyKind(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string)) return PropertyKind.String;
        if (underlying == typeof(bool)) return PropertyKind.Boolean;
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return PropertyKind.DateTime;
        if (underlying.IsValueType && (underlying == typeof(int) || underlying == typeof(long)
            || underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal)))
            return PropertyKind.Number;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(underlying) && underlying != typeof(string))
            return PropertyKind.Array;
        if (underlying.IsClass) return PropertyKind.Object;

        return PropertyKind.String;
    }

    private static Type? GetInnerType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsClass && underlying != typeof(string) ? underlying : null;
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1) return args[0];
        }
        return null;
    }

    private static List<PropertyInfo> GetCommonOsduProperties()
    {
        return
        [
            new() { Name = "id", JsonName = "id", Path = "id", Kind = PropertyKind.String },
            new() { Name = "kind", JsonName = "kind", Path = "kind", Kind = PropertyKind.String },
            new() { Name = "version", JsonName = "version", Path = "version", Kind = PropertyKind.Number },
            new() { Name = "createTime", JsonName = "createTime", Path = "createTime", Kind = PropertyKind.DateTime },
            new() { Name = "modifyTime", JsonName = "modifyTime", Path = "modifyTime", Kind = PropertyKind.DateTime },
            new() { Name = "createUser", JsonName = "createUser", Path = "createUser", Kind = PropertyKind.String },
            new() { Name = "modifyUser", JsonName = "modifyUser", Path = "modifyUser", Kind = PropertyKind.String },
            new() { Name = "acl", JsonName = "acl", Path = "acl", Kind = PropertyKind.Object, Children =
            [
                new() { Name = "viewers", JsonName = "viewers", Path = "acl.viewers", Kind = PropertyKind.Array },
                new() { Name = "owners", JsonName = "owners", Path = "acl.owners", Kind = PropertyKind.Array },
            ]},
            new() { Name = "legal", JsonName = "legal", Path = "legal", Kind = PropertyKind.Object, Children =
            [
                new() { Name = "legaltags", JsonName = "legaltags", Path = "legal.legaltags", Kind = PropertyKind.Array },
                new() { Name = "otherRelevantDataCountries", JsonName = "otherRelevantDataCountries", Path = "legal.otherRelevantDataCountries", Kind = PropertyKind.Array },
            ]},
            new() { Name = "data", JsonName = "data", Path = "data", Kind = PropertyKind.Object },
        ];
    }
}