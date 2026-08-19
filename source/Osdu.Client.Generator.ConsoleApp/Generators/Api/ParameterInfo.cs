namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Represents a resolved parameter for an API method.
/// </summary>
public class ParameterInfo
{
    public string OriginalName { get; init; } = "";
    public string CSharpName { get; init; } = "";
    public string Type { get; init; } = "";
    public string Location { get; init; } = "";
    public bool IsRequired { get; init; }
    public string? DefaultValue { get; init; }
}
