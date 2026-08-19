namespace Osdu.Client.Extensions.Caching;

/// <summary>
/// Describes an OSDU cache registration: the item type, kind pattern, and options.
/// The <see cref="KeyPrefix"/> is automatically derived from <see cref="ItemType"/> name
/// (e.g. <c>Wellbore_1_3_0</c> → <c>osdu:wellbore</c>).
/// </summary>
public class OsduCacheDescriptor
{
    /// <summary>
    /// Cache key prefix, auto-generated from <see cref="ItemType"/> if not explicitly set.
    /// </summary>
    public string KeyPrefix => $"osdu:{DeriveEntityName(ItemType)}";

    public required string Kind { get; init; }
    public required CacheOptions Options { get; init; }
    public required Type ItemType { get; init; }

    /// <summary>
    /// Derives a clean entity name from a schema type name.
    /// <c>Wellbore_1_3_0</c> → <c>wellbore</c>,
    /// <c>UnitOfMeasure_1_0_0</c> → <c>unitofmeasure</c>,
    /// <c>Well_1_0_0</c> → <c>well</c>.
    /// </summary>
    private static string DeriveEntityName(Type type)
    {
        var name = type.Name;

        //// Strip version suffix like _1_3_0
        //var firstUnderscore = name.IndexOf('_');
        //if (firstUnderscore > 0)
        //    name = name[..firstUnderscore];

        return name.ToLowerInvariant();
    }
}
