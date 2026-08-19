using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Builds OSDU/Lucene query strings from strongly-typed lambda expressions.
/// <para>Supported operations:</para>
/// <list type="bullet">
///   <item><c>==</c>, <c>!=</c> — equality / negation</item>
///   <item><c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c> — range queries</item>
///   <item><c>&amp;&amp;</c>, <c>||</c>, <c>!</c> — logical AND, OR, NOT</item>
///   <item><c>.Contains()</c> — wildcard <c>*value*</c></item>
///   <item><c>.StartsWith()</c> — prefix <c>value*</c></item>
///   <item><c>.EndsWith()</c> — suffix <c>*value</c></item>
///   <item><c>.IsNull()</c> — field does not exist</item>
///   <item><c>.IsNotNull()</c> / <c>.Exists()</c> — field exists</item>
///   <item><c>.IsOneOf()</c> — multi-value OR</item>
///   <item><c>.Between()</c> — inclusive range <c>[min TO max]</c></item>
///   <item><c>.MatchesPattern()</c> — raw wildcard/regex pattern</item>
///   <item><c>.Fuzzy()</c> — fuzzy search <c>value~distance</c></item>
///   <item><c>== null</c> / <c>!= null</c> — null checks</item>
/// </list>
/// </summary>
public static class OsduQueryBuilder
{
    /// <summary>
    /// Converts a predicate expression into an OSDU-compatible Lucene query string.
    /// </summary>
    public static string Build<TItem>(Expression<Func<TItem, bool>> predicate)
    {
        return ParseExpression(predicate.Body);
    }

    private static string ParseExpression(Expression expression)
    {
        return expression switch
        {
            BinaryExpression binary => ParseBinary(binary),
            UnaryExpression { NodeType: ExpressionType.Not } unary => $"NOT ({ParseExpression(unary.Operand)})",
            MethodCallExpression method => ParseMethodCall(method),
            _ => throw new NotSupportedException($"Expression type '{expression.NodeType}' is not supported.")
        };
    }

    private static string ParseBinary(BinaryExpression binary)
    {
        // Logical operators: AND / OR
        if (binary.NodeType is ExpressionType.AndAlso)
            return $"({ParseExpression(binary.Left)} AND {ParseExpression(binary.Right)})";

        if (binary.NodeType is ExpressionType.OrElse)
            return $"({ParseExpression(binary.Left)} OR {ParseExpression(binary.Right)})";

        // Null checks: w.Data.Field == null  /  w.Data.Field != null
        if (IsNullConstant(binary.Right))
        {
            var fieldPath = ResolveMemberPath(binary.Left);
            return binary.NodeType switch
            {
                ExpressionType.Equal => $"NOT _exists_:{fieldPath}",
                ExpressionType.NotEqual => $"_exists_:{fieldPath}",
                _ => throw new NotSupportedException($"Null comparison with '{binary.NodeType}' is not supported.")
            };
        }

        if (IsNullConstant(binary.Left))
        {
            var fieldPath = ResolveMemberPath(binary.Right);
            return binary.NodeType switch
            {
                ExpressionType.Equal => $"NOT _exists_:{fieldPath}",
                ExpressionType.NotEqual => $"_exists_:{fieldPath}",
                _ => throw new NotSupportedException($"Null comparison with '{binary.NodeType}' is not supported.")
            };
        }

        // Comparison operators
        var field = ResolveMemberPath(binary.Left);
        var value = ResolveValue(binary.Right);

        return binary.NodeType switch
        {
            ExpressionType.Equal => $"{field}:\"{value}\"",
            ExpressionType.NotEqual => $"NOT {field}:\"{value}\"",
            ExpressionType.GreaterThan => $"{field}:{{{value} TO *}}",
            ExpressionType.GreaterThanOrEqual => $"{field}:[{value} TO *]",
            ExpressionType.LessThan => $"{field}:{{* TO {value}}}",
            ExpressionType.LessThanOrEqual => $"{field}:[* TO {value}]",
            _ => throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported.")
        };
    }

    private static string ParseMethodCall(MethodCallExpression method)
    {
        // string.Contains (instance method)
        if (method.Method.Name == nameof(string.Contains) && method.Object is not null)
        {
            var fieldPath = ResolveMemberPath(method.Object);
            var value = EscapeLucene(ResolveValue(method.Arguments[0]));
            return $"{fieldPath}:*{value}*";
        }

        // string.StartsWith (instance method)
        if (method.Method.Name == nameof(string.StartsWith) && method.Object is not null)
        {
            var fieldPath = ResolveMemberPath(method.Object);
            var value = EscapeLucene(ResolveValue(method.Arguments[0]));
            return $"{fieldPath}:{value}*";
        }

        // string.EndsWith (instance method)
        if (method.Method.Name == nameof(string.EndsWith) && method.Object is not null)
        {
            var fieldPath = ResolveMemberPath(method.Object);
            var value = EscapeLucene(ResolveValue(method.Arguments[0]));
            return $"{fieldPath}:*{value}";
        }

        // Extension methods — resolved by name
        return method.Method.Name switch
        {
            nameof(OsduQueryExtensions.Exists) => ParseFieldOnlyExtension(method, field => $"_exists_:{field}"),
            nameof(OsduQueryExtensions.IsNotNull) => ParseFieldOnlyExtension(method, field => $"_exists_:{field}"),
            nameof(OsduQueryExtensions.IsNull) => ParseFieldOnlyExtension(method, field => $"NOT _exists_:{field}"),
            nameof(OsduQueryExtensions.IsOneOf) => ParseIsOneOf(method),
            nameof(OsduQueryExtensions.Between) => ParseBetween(method),
            nameof(OsduQueryExtensions.MatchesPattern) => ParseSingleArgExtension(method, (field, value) =>
                value.Contains('*') || value.Contains('?')
                    ? $"{field}:{value}"
                    : $"{field}:\"{value}\""),
            nameof(OsduQueryExtensions.Fuzzy) => ParseFuzzy(method),
            _ => throw new NotSupportedException($"Method '{method.Method.Name}' is not supported.")
        };
    }

    private static string ParseFieldOnlyExtension(MethodCallExpression method, Func<string, string> format)
    {
        var fieldPath = ResolveMemberPath(method.Arguments[0]);
        return format(fieldPath);
    }

    private static string ParseSingleArgExtension(MethodCallExpression method, Func<string, string, string> format)
    {
        var fieldPath = ResolveMemberPath(method.Arguments[0]);
        var value = ResolveValue(method.Arguments[1]);
        return format(fieldPath, value);
    }

    private static string ParseIsOneOf(MethodCallExpression method)
    {
        var fieldPath = ResolveMemberPath(method.Arguments[0]);
        var values = ResolveValues(method.Arguments[1]);
        var terms = string.Join(" OR ", values.Select(v => $"{fieldPath}:\"{v}\""));
        return $"({terms})";
    }

    private static string ParseBetween(MethodCallExpression method)
    {
        var fieldPath = ResolveMemberPath(method.Arguments[0]);
        var min = ResolveValue(method.Arguments[1]);
        var max = ResolveValue(method.Arguments[2]);
        return $"{fieldPath}:[{min} TO {max}]";
    }

    private static string ParseFuzzy(MethodCallExpression method)
    {
        var fieldPath = ResolveMemberPath(method.Arguments[0]);
        var value = ResolveValue(method.Arguments[1]);
        var distance = method.Arguments.Count > 2 ? ResolveValue(method.Arguments[2]) : "2";
        return $"{fieldPath}:{value}~{distance}";
    }

    /// <summary>
    /// Resolves a member expression chain to a dot-separated OSDU field path,
    /// using <see cref="JsonPropertyNameAttribute"/> when present.
    /// </summary>
    private static string ResolveMemberPath(Expression expression)
    {
        var parts = new List<string>();
        var current = expression;

        // Unwrap convert/cast nodes
        while (current is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            current = unary.Operand;

        while (current is MemberExpression member)
        {
            var jsonName = member.Member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? member.Member.Name;
            parts.Add(jsonName);
            current = member.Expression;
        }

        parts.Reverse();

        // Skip the root parameter (e.g. "w" in w => w.Data.WellID)
        return string.Join(".", parts);
    }

    private static bool IsNullConstant(Expression expression) =>
        expression is ConstantExpression { Value: null }
        || (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary
            && unary.Operand is ConstantExpression { Value: null });

    private static string ResolveValue(Expression expression)
    {
        var value = Expression.Lambda(expression).Compile().DynamicInvoke();
        return value?.ToString() ?? "";
    }

    private static List<string> ResolveValues(Expression expression)
    {
        var value = Expression.Lambda(expression).Compile().DynamicInvoke();
        return value switch
        {
            IEnumerable<string> strings => strings.ToList(),
            _ => throw new NotSupportedException("IsOneOf requires an IEnumerable<string> argument.")
        };
    }

    private static string EscapeLucene(string value)
    {
        // Escape Lucene special characters, preserving * and ? as wildcards
        char[] specialChars = ['+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', ':', '\\', '/'];
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (specialChars.Contains(c))
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
