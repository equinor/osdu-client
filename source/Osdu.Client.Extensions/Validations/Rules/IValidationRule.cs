using Osdu.Client.Extensions.Caching;

namespace Osdu.Client.Extensions.Validations.Rules
{
    /// <summary>
    /// A validation rule that checks value(s) against a pre-loaded set of known values.
    /// </summary>
    internal interface IValidationRule
    {
        Type ReferenceType { get; }
        Task EnsureCacheLoadedAsync(Dictionary<Type, HashSet<string>> cache, IOsduCacheProvider provider, CancellationToken ct);
        OsduValidationResult Execute(Dictionary<Type, HashSet<string>> cache);
    }
}
