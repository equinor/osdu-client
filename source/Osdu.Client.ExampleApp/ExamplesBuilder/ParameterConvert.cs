using System;
using System.Windows;
using System.Windows.Controls;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Utility for reading, converting, and displaying parameter values.
/// </summary>
internal static class ParameterConvert
{
    public static string GetValue(FrameworkElement control) => control switch
    {
        TextBox tb => tb.Text,
        CheckBox cb => cb.IsChecked == true ? "true" : "false",
        _ => ""
    };

    public static object? Convert(string rawValue, Type targetType)
    {
        if (targetType == typeof(string)) return rawValue;
        if (targetType == typeof(string[])) return rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (targetType == typeof(bool)) return bool.Parse(rawValue);
        if (targetType == typeof(int)) return int.Parse(rawValue);
        if (targetType == typeof(long)) return long.Parse(rawValue);
        if (targetType == typeof(double)) return double.Parse(rawValue);
        if (targetType == typeof(float)) return float.Parse(rawValue);
        if (targetType == typeof(decimal)) return decimal.Parse(rawValue);
        if (targetType == typeof(int?)) return string.IsNullOrWhiteSpace(rawValue) ? null : int.Parse(rawValue);
        if (targetType == typeof(double?)) return string.IsNullOrWhiteSpace(rawValue) ? null : double.Parse(rawValue);
        if (targetType == typeof(bool?)) return string.IsNullOrWhiteSpace(rawValue) ? null : bool.Parse(rawValue);
        return System.Convert.ChangeType(rawValue, targetType);
    }

    public static string FriendlyTypeName(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(string[]) => "string[]",
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "long",
        _ when type == typeof(double) => "double",
        _ when type == typeof(float) => "float",
        _ when type == typeof(decimal) => "decimal",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(int?) => "int?",
        _ when type == typeof(double?) => "double?",
        _ when type == typeof(bool?) => "bool?",
        _ => type.Name
    };
}
