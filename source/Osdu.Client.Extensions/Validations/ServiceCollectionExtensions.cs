using Microsoft.Extensions.DependencyInjection;
using Osdu.Client.Extensions.Caching;

namespace Osdu.Client.Extensions.Validations;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOsduDataValidator"/>.
    /// Automatically calls <see cref="Caching.ServiceCollectionExtensions.AddOsduCaching"/>
    /// if <see cref="IOsduCacheProvider"/> is not already registered.
    /// </summary>
    public static IServiceCollection AddOsduDataValidators(this IServiceCollection services)
    {
        if (!services.Any(s => s.ServiceType == typeof(IOsduCacheProvider)))
        {
            throw new InvalidOperationException($"'{nameof(IOsduCacheProvider)}' is not registered. " + $"Call 'AddOsduCaching()' before 'AddOsduDataValidators()'.");
        }

        services.AddSingleton<IOsduDataValidator, OsduDataValidator>();
        return services;
    }
}  