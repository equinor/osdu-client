namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Extension methods used inside OSDU query expressions.
/// These are never actually executed — they are parsed by <see cref="OsduQueryBuilder"/>.
/// </summary>
public static class OsduQueryExtensions
{
    /// <summary>
    /// Matches values that start with the given prefix. Translates to <c>field:value*</c>.
    /// </summary>
    public static bool StartsWith(this string field, string prefix) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Matches values that end with the given suffix. Translates to <c>field:*value</c>.
    /// </summary>
    public static bool EndsWith(this string field, string suffix) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Checks that the field exists (is not null). Translates to <c>_exists_:field</c>.
    /// </summary>
    public static bool Exists(this object? field) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Checks that the field is null (does not exist). Translates to <c>NOT _exists_:field</c>.
    /// </summary>
    public static bool IsNull(this object? field) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Checks that the field is not null (exists). Translates to <c>_exists_:field</c>.
    /// Alias for <see cref="Exists"/>.
    /// </summary>
    public static bool IsNotNull(this object? field) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Matches any of the provided values. Translates to <c>(field:"v1" OR field:"v2")</c>.
    /// </summary>
    public static bool IsOneOf(this string field, IEnumerable<string> values) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Matches values within an inclusive range. Translates to <c>field:[min TO max]</c>.
    /// </summary>
    public static bool Between<T>(this T field, T min, T max) where T : IComparable<T> =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Matches values using a wildcard/regex pattern. Translates to <c>field:pattern</c>.
    /// </summary>
    public static bool MatchesPattern(this string field, string pattern) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");

    /// <summary>
    /// Matches using fuzzy search with optional edit distance. Translates to <c>field:value~distance</c>.
    /// </summary>
    public static bool Fuzzy(this string field, string value, int distance = 2) =>
        throw new NotSupportedException("This method is only for use in OsduQueryBuilder expressions.");
}
