using System.Text.Json;

namespace Osdu.Client.Extensions.Extensions;

/// <summary>
/// Extension methods for deserializing untyped results to strongly-typed objects.
/// </summary>
public static class ObjectExtensions
{
    /// <summary>
    /// Deserializes an object (typically a <see cref="JsonElement"/>) to the specified type.
    /// </summary>
    public static T Deserialize<T>(this object obj, JsonSerializerOptions? options = null)
    {
        return obj switch
        {
            JsonElement element => JsonSerializer.Deserialize<T>(element, options)!,
            T typed => typed,
            _ => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, options), options)!
        };
    }

    /// <summary>
    /// Deserializes a list of objects to a strongly-typed list.
    /// </summary>
    public static List<T> Deserialize<T>(this IEnumerable<object> objects, JsonSerializerOptions? options = null)
    {
        return objects.Select(obj => obj.Deserialize<T>(options)).ToList();
    }
}
