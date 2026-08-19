using System;
using System.Collections.Generic;
using System.Text;

namespace Osdu.Client.Generator.ConsoleApp.Extensions;

public static class ConsoleEx
{
    public static void WriteGreen(string message)
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteRed(string message)
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}
