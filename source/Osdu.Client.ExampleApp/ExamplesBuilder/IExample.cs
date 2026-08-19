using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

/// <summary>
/// Represents a runnable OSDU client example.
/// </summary>
public interface IExample
{
    /// <summary>
    /// Display name shown on the sidebar button.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Short description shown below the title when selected.
    /// </summary>
    string ShortDescription { get; }

    /// <summary>
    /// Category used to group examples in the sidebar.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// The source code of the RunAsync method for display purposes.
    /// </summary>
    string SourceCode { get; }

    /// <summary>
    /// The full source code of the example class file.
    /// </summary>
    string FullSourceCode { get; }

    /// <summary>
    /// Returns metadata for all user-configurable parameters on this example.
    /// </summary>
    IReadOnlyList<ExampleParameterInfo> Parameters { get; }

    /// <summary>
    /// Executes the example and returns the result as a string.
    /// </summary>
    Task<string> RunAsync(CancellationToken cancellationToken = default);
}
