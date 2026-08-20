using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Osdu.Client.Apis;
using Osdu.Client.Authentication;
using Osdu.Client.Data.ReferenceData;
using Osdu.Client.ExampleApp.Examples;
using Osdu.Client.Extensions.Caching;
using Osdu.Client.Extensions.Querying;
using System.Reflection;
using System.Windows;


namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();

        string scope = configuration["OsduScope"]!;
        string baseUrl = configuration["OsduBaseUrl"]!;
        string dataPartitionId = configuration["OsduDataPartitionId"]!;
        string tenantId = configuration["AzureTenantId"]!;
        string clientId = configuration["OsduClientId"]!;
        string clientSecret = configuration["OsduClientSecret"]!;

        var services = new ServiceCollection();

        services.AddTransient<OsduAuthHandler>(_ => new OsduAuthHandler(tenantId, clientId, clientSecret, scope));
        services.AddOsduApiClients(
            httpClient =>
            {
                httpClient.BaseAddress = new Uri(baseUrl);
                httpClient.DefaultRequestHeaders.Add("data-partition-id", dataPartitionId);
            },
            httpClientBuilder =>
            {
                httpClientBuilder.AddHttpMessageHandler<OsduAuthHandler>();
            });

        // Register a named HttpClient for the dynamic API explorer
        services.AddHttpClient("OsduApi", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("data-partition-id", dataPartitionId);
        }).AddHttpMessageHandler<OsduAuthHandler>();

        // Configure caching
        services.AddOsduCaching(configuration, configureDescriptors: descriptors =>
        {
            descriptors.Add(new OsduCacheDescriptor
            {
                Kind = "osdu:wks:reference-data--UnitOfMeasure:1.0.0",
                Options = new CacheOptions { Expiration = TimeSpan.FromHours(1), CacheAll = true },
                ItemType = typeof(UnitOfMeasure_1_0_0)
            });

            descriptors.Add(new OsduCacheDescriptor
            {
                Kind = "osdu:wks:reference-data--SampleImageColourSpace:1.0.0",
                Options = new CacheOptions { Expiration = TimeSpan.FromHours(1), CacheAll = true },
                ItemType = typeof(SampleImageColourSpace_1_0_0)
            });
        });

        services.AddOsduQueryExecutor();

        // Auto-discover and register all IExample implementations
        var exampleTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IExample)));

        foreach (var type in exampleTypes)
        {
            services.AddTransient(typeof(IExample), type);
        }

        services.AddTransient<MainWindow>();
        services.AddTransient<ApiTestWindow>();
        services.AddTransient<DataBrowserWindow>();

        Services = services.BuildServiceProvider();

        //var window = Services.GetRequiredService<ApiTestWindow>();
        var window = Services.GetRequiredService<DataBrowserWindow>();
        window.Show();
    }
}
