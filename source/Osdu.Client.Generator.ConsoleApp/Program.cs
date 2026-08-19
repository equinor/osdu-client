using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Osdu.Client.Generator.ConsoleApp.Generators;

namespace Osdu.Client.Generator.ConsoleApp;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine($"""
                          ========================================================================================================================================
                          OSDU Client Generator:                                                                      
                          
                          A tool for generating C# API clients and Domain Data models classes from OSDU Api & Domain Data OpenApi specification files (*.json)
                          
                          - API definitions are used to generate strongly-typed api clients with related request/response model classes,
                          - Data definitions are used to generate strongly-typed Domain data model classes
                          
                          The output will be placed in the Osdu.Client project.
                          ========================================================================================================================================
                          """);

        ServiceProvider serviceProvider = ConfigureServices()
            .WithConfiguration()
            .WithLogging()
            .WithGenerators()
            .Build();

        ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        CodeGenerator codeGenerator = serviceProvider.GetRequiredService<CodeGenerator>();

        logger.LogInformation("Starting OSDU Client generator...");

        codeGenerator.Generate();

        logger.LogInformation("Finished!!!");
        Console.ReadLine();
    }

    static ServiceCollectionBuilder ConfigureServices()
    {
        IServiceCollection services = new ServiceCollection();
        return new ServiceCollectionBuilder(services);
    }

}
