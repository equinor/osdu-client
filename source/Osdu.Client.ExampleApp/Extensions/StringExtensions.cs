using System;
using System.Collections.Generic;
using System.Text;

namespace Osdu.Client.ExampleApp.Extensions;

public static class StringExtensions
{
    public static string RemoveExample(this string input )
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        if (input.EndsWith("Example", StringComparison.OrdinalIgnoreCase))
        {
            return input.Substring(0, input.Length - "Example".Length);
        }

        return input;
    }
}
