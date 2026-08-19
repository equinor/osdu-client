using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Marks a property on an <see cref="IExample"/> as a user-configurable parameter.
/// The UI will generate an input control for each decorated property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ExampleParameterAttribute : Attribute
{
    /// <summary>
    /// Display label for the parameter in the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Help text shown below the input control.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Whether a value must be provided before running.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Display order in the UI (lower = first).
    /// </summary>
    public int Order { get; init; }
}
