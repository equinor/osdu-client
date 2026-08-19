using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Osdu.Client.Extensions.Querying;

/// <summary>
/// Resolves strongly-typed member expressions to OSDU field paths
/// using <see cref="JsonPropertyNameAttribute"/> when present.
/// </summary>
public static class OsduFieldSelector
{
    /// <summary>
    /// Resolves a single member expression to an OSDU field path.
    /// </summary>
    public static string Resolve<TItem>(Expression<Func<TItem, object?>> selector)
    {
        return ResolveMemberPath(selector.Body);
    }

    /// <summary>
    /// Resolves multiple member expressions to OSDU field paths.
    /// </summary>
    public static List<string> ResolveMany<TItem>(params Expression<Func<TItem, object?>>[] selectors)
    {
        return selectors.Select(s => Resolve(s)).ToList();
    }

    private static string ResolveMemberPath(Expression expression)
    {
        var parts = new List<string>();
        var current = expression;

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
        return string.Join(".", parts);
    }
}
