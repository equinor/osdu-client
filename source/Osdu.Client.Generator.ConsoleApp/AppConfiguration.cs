using System.Runtime.CompilerServices;

namespace Osdu.Client.Generator.ConsoleApp;

public class AppConfiguration
{
    public string ApplicationDir { get; private set; }
    public string OutputBaseDir { get; private set; }

    public ApiConfiguration Api { get; init; }

    public ExtensionConfiguration Extension { get; init; }

    public ConverterConfiguration Converter { get; init; }

    public SchemaConfiguration Schema { get; init; }

    public void ResolvePaths()
    {
        string appDir = GetAppDirectory();
        string parentDir = Directory.GetParent(appDir)?.FullName ?? throw new InvalidOperationException("Failed to get parent directory of source directory.");

        ApplicationDir = appDir;
        OutputBaseDir = Path.Combine(parentDir, "Osdu.Client");

        Api.DefinitionsDir = Path.Combine(appDir, Api.DefinitionsDir);
        Api.OutputDir = Path.Combine(OutputBaseDir, Api.OutputDir);

        Extension.OutputDir = Path.Combine(OutputBaseDir, Extension.OutputDir);

        Converter.OutputDir = Path.Combine(OutputBaseDir, Converter.OutputDir);

        Schema.DefinitionsDir = Path.Combine(appDir, Schema.DefinitionsDir);
        Schema.OutputDir = Path.Combine(OutputBaseDir, Schema.OutputDir);
    }

    static string GetAppDirectory([CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetDirectoryName(sourceFilePath)!;
    }
}

public class ApiConfiguration
{
    public required string DefinitionsDir { get; set; }

    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}

public class SchemaConfiguration
{
    public required string DefinitionsDir { get; set; }

    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}

public class ExtensionConfiguration
{
    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}

public class ConverterConfiguration
{
    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}
