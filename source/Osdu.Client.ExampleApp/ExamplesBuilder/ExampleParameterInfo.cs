using System.Reflection;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Metadata about a single parameter on an example, discovered via reflection.
/// </summary>
public sealed class ExampleParameterInfo
{
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required bool Required { get; init; }
    public required int Order { get; init; }
    public required PropertyInfo Property { get; init; }
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Gets the current value of the parameter from the example instance.
    /// </summary>
    public object? GetValue(IExample example) => Property.GetValue(example);

    /// <summary>
    /// Sets the value of the parameter on the example instance.
    /// </summary>
    public void SetValue(IExample example, object? value) => Property.SetValue(example, value);
}
