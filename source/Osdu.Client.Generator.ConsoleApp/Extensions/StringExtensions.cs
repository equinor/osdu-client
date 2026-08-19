using System.Text;

namespace Osdu.Client.Generator.ConsoleApp.Extensions;

public static class StringExtensions
{
    //public static string ToPascalCase(this string input)
    //{
    //    if (string.IsNullOrEmpty(input)) return input;

    //    var parts = input.Split(['_', '-', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
    //    var sb = new StringBuilder();
    //    foreach (var part in parts)
    //    {
    //        if (part.Length > 0)
    //        {
    //            sb.Append(char.ToUpperInvariant(part[0]));
    //            sb.Append(part[1..]);
    //        }
    //    }

    //    // Ensure first character is always uppercase even if no split occurred
    //    if (sb.Length > 0 && char.IsLower(sb[0]))
    //        sb[0] = char.ToUpperInvariant(sb[0]);

    //    return sb.ToString();
    //}

    public static string ToPascalCase(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var parts = input.Split(['_', '-', ' ', '.', ':'], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                sb.Append(part[1..]);
            }
        }

        // Ensure first character is always uppercase even if no split occurred
        if (sb.Length > 0 && char.IsLower(sb[0]))
            sb[0] = char.ToUpperInvariant(sb[0]);

        return sb.ToString();
    }
}
