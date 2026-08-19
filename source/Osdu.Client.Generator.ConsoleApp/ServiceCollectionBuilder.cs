using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Osdu.Client.Generator.ConsoleApp.Generators;
using Osdu.Client.Generator.ConsoleApp.Generators.Api;
using Osdu.Client.Generator.ConsoleApp.Generators.Schema;
using Serilog;

namespace Osdu.Client.Generator.ConsoleApp;


internal class ServiceCollectionBuilder
{
    private readonly IServiceCollection _services;
    private IConfiguration? _configuration;

    public ServiceCollectionBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public ServiceCollectionBuilder WithGenerators()
    {
        _services.AddScoped<CodeGenerator>();
        _services.AddScoped<ApiGenerator>();
        _services.AddScoped<SchemaGenerator>();
        _services.AddScoped<ServiceCollectionExtensionsGenerator>();
        _services.AddScoped<OsduClientGenerator>();

        return this;
    }

    public ServiceCollectionBuilder WithConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        AppConfiguration configuration = _configuration.Get<AppConfiguration>()
                                         ?? throw new InvalidOperationException("Failed to bind configuration.");

        configuration.ResolvePaths();

        _services.AddSingleton(configuration);
        _services.AddSingleton(configuration.Api);
        _services.AddSingleton(configuration.Data);

        return this;
    }


    public ServiceCollectionBuilder WithLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(_configuration ?? throw new InvalidOperationException("Configuration must be set before logging. Call WithConfiguration() first."))
            .CreateLogger();

        _services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return this;
    }

    public ServiceProvider Build()
    {
        return _services.BuildServiceProvider();
    }
}
