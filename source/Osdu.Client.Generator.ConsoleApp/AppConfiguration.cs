using System.Runtime.CompilerServices;

namespace Osdu.Client.Generator.ConsoleApp;

public class AppConfiguration
{
    public string ApplicationDir { get; private set; }
    public string OutputBaseDir { get; private set; }

    public ApiConfiguration Api { get; init; }

    public DataConfiguration Data { get; init; }

    public void ResolvePaths()
    {
        string appDir = GetAppDirectory();
        string parentDir = Directory.GetParent(appDir)?.FullName ?? throw new InvalidOperationException("Failed to get parent directory of source directory.");

        ApplicationDir = appDir;
        OutputBaseDir = Path.Combine(parentDir, "Osdu.Client");

        Api.DefinitionsDir = Path.Combine(appDir, Api.DefinitionsDir);
        Api.OutputDir = Path.Combine(OutputBaseDir, Api.OutputDir);

        Data.DefinitionsDir = Path.Combine(appDir, Data.DefinitionsDir);
        Data.OutputDir = Path.Combine(OutputBaseDir, Data.OutputDir);
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

public class DataConfiguration
{
    public required string DefinitionsDir { get; set; }

    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}