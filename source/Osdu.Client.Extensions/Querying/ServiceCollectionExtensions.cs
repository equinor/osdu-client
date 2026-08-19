using Microsoft.Extensions.DependencyInjection;

namespace Osdu.Client.Extensions.Querying;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers only the OSDU query executor (without caching).
    /// Use when you need strongly-typed queries but don't want caching overhead.
    /// </summary>
    public static IServiceCollection AddOsduQueryExecutor(this IServiceCollection services)
    {
        services.AddSingleton<IOsduQueryExecutor, OsduQueryExecutor>();
        return services;
    }
}
