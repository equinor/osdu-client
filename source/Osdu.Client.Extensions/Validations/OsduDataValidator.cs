using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Osdu.Client.Extensions.Caching;
using Osdu.Client.Extensions.Validations.Rules;

namespace Osdu.Client.Extensions.Validations;

/// <inheritdoc />
public sealed class OsduDataValidator(IOsduCacheProvider cacheProvider, ILogger<OsduDataValidator> logger) : IOsduDataValidator
{
    private static readonly ConcurrentDictionary<string, Delegate> CompiledExpressions = [];

    /// <inheritdoc />
    public IOsduDataValidator.IValidationBuilder<TRecord> For<TRecord>(TRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new ValidationBuilder<TRecord>(cacheProvider, logger, record);
    }

    /// <summary>
    /// Compiles and caches an expression delegate keyed by type + expression tree.
    /// </summary>
    internal static Func<T, TResult> CompileAndCache<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        var key = $"{typeof(T).FullName}|{typeof(TResult).FullName}|{expression}";
        return (Func<T, TResult>)CompiledExpressions.GetOrAdd(key, _ => expression.Compile());
    }

    /// <summary>
    /// Extracts a field path from a member expression.
    /// <c>x => x.Data.ExistenceKind</c> → <c>"Data.ExistenceKind"</c>.
    /// </summary>
    internal static string GetFieldName(LambdaExpression expression) =>
        expression.Body switch
        {
            MemberExpression member => BuildMemberPath(member),
            _ => expression.ToString()
        };

    private static string BuildMemberPath(MemberExpression expression)
    {
        var parts = new List<string>();
        Expression? current = expression;

        while (current is MemberExpression member)
        {
            parts.Add(member.Member.Name);
            current = member.Expression;
        }

        parts.Reverse();
        return string.Join(".", parts);
    }

    ///// <summary>
    ///// A validation rule that checks value(s) against a pre-loaded set of known values.
    ///// </summary>
    //private interface IValidationRule
    //{
    //    Type ReferenceType { get; }
    //    Task EnsureCacheLoadedAsync(Dictionary<Type, HashSet<string>> cache, IOsduCacheProvider provider, CancellationToken ct);
    //    OsduValidationResult Execute(Dictionary<Type, HashSet<string>> cache);
    //}

    //private sealed class SingleRule<TRecord, TRef> : IValidationRule
    //{
    //    private readonly TRecord _record;
    //    private readonly Func<TRecord, string?> _valueSelector;
    //    private readonly Func<TRef, string?> _matchFieldSelector;
    //    private readonly string _fieldName;

    //    public SingleRule(TRecord record, Expression<Func<TRecord, string?>> valueSelector, Expression<Func<TRef, string?>> matchField)
    //    {
    //        _record = record;
    //        _valueSelector = CompileAndCache(valueSelector);
    //        _matchFieldSelector = CompileAndCache(matchField);
    //        _fieldName = GetFieldName(valueSelector);
    //    }

    //    public Type ReferenceType => typeof(TRef);

    //    public async Task EnsureCacheLoadedAsync(Dictionary<Type, HashSet<string>> cache, IOsduCacheProvider provider, CancellationToken ct)
    //    {
    //        if (cache.ContainsKey(typeof(TRef))) return;

    //        var items = await provider.GetAllAsync<TRef>(ct);
    //        cache[typeof(TRef)] = new HashSet<string>(
    //            items.Select(_matchFieldSelector).Where(v => v is not null)!,
    //            StringComparer.OrdinalIgnoreCase);
    //    }

    //    public OsduValidationResult Execute(Dictionary<Type, HashSet<string>> cache)
    //    {
    //        var value = _valueSelector(_record);
    //        if (string.IsNullOrWhiteSpace(value))
    //            return OsduValidationResult.Success();

    //        return cache[typeof(TRef)].Contains(value)
    //            ? OsduValidationResult.Success()
    //            : OsduValidationResult.Failure(new OsduValidationError
    //            {
    //                Value = value,
    //                ReferenceType = typeof(TRef).Name,
    //                FieldName = _fieldName
    //            });
    //    }
    //}

    //private sealed class CollectionRule<TRecord, TRef, TItem> : IValidationRule
    //{
    //    private readonly TRecord _record;
    //    private readonly Func<TRecord, IEnumerable<TItem>?> _collectionSelector;
    //    private readonly Func<TItem, string?> _itemValueSelector;
    //    private readonly Func<TRef, string?> _matchFieldSelector;
    //    private readonly string _fieldName;

    //    public CollectionRule(TRecord record, Expression<Func<TRecord, IEnumerable<TItem>?>> collectionSelector, Expression<Func<TItem, string?>> itemValueSelector, Expression<Func<TRef, string?>> matchField)
    //    {
    //        _record = record;
    //        _collectionSelector = CompileAndCache(collectionSelector);
    //        _itemValueSelector = CompileAndCache(itemValueSelector);
    //        _matchFieldSelector = CompileAndCache(matchField);
    //        _fieldName = $"{GetFieldName(collectionSelector)}.{GetFieldName(itemValueSelector)}";
    //    }

    //    public Type ReferenceType => typeof(TRef);

    //    public async Task EnsureCacheLoadedAsync(Dictionary<Type, HashSet<string>> cache, IOsduCacheProvider provider, CancellationToken ct)
    //    {
    //        if (cache.ContainsKey(typeof(TRef))) return;

    //        var items = await provider.GetAllAsync<TRef>(ct);
    //        cache[typeof(TRef)] = new HashSet<string>(
    //            items.Select(_matchFieldSelector).Where(v => v is not null)!,
    //            StringComparer.OrdinalIgnoreCase);
    //    }

    //    public OsduValidationResult Execute(Dictionary<Type, HashSet<string>> cache)
    //    {
    //        var collection = _collectionSelector(_record);
    //        if (collection is null)
    //            return OsduValidationResult.Success();

    //        var knownValues = cache[typeof(TRef)];

    //        var errors = collection
    //            .Select(_itemValueSelector)
    //            .Where(v => !string.IsNullOrWhiteSpace(v))
    //            .Distinct(StringComparer.OrdinalIgnoreCase)
    //            .Where(v => !knownValues.Contains(v!))
    //            .Select(v => new OsduValidationError
    //            {
    //                Value = v,
    //                ReferenceType = typeof(TRef).Name,
    //                FieldName = _fieldName
    //            })
    //            .ToList();

    //        return errors.Count == 0
    //            ? OsduValidationResult.Success()
    //            : new OsduValidationResult { Errors = errors };
    //    }
    //}

    private sealed class ValidationBuilder<TRecord>(IOsduCacheProvider cacheProvider, ILogger logger, TRecord record) : IOsduDataValidator.IValidationBuilder<TRecord>
    {
        private readonly List<IValidationRule> _rules = [];

        public IOsduDataValidator.IValidationBuilder<TRecord> Validate<TRef>(Expression<Func<TRecord, string?>> valueSelector, Expression<Func<TRef, string?>> matchField)
        {
            ArgumentNullException.ThrowIfNull(valueSelector);
            ArgumentNullException.ThrowIfNull(matchField);
            _rules.Add(new SingleRule<TRecord, TRef>(record, valueSelector, matchField));
            return this;
        }

        public IOsduDataValidator.IValidationBuilder<TRecord> ValidateAll<TRef, TItem>(Expression<Func<TRecord, IEnumerable<TItem>?>> collectionSelector, Expression<Func<TItem, string?>> itemValueSelector, Expression<Func<TRef, string?>> matchField)
        {
            ArgumentNullException.ThrowIfNull(collectionSelector);
            ArgumentNullException.ThrowIfNull(itemValueSelector);
            ArgumentNullException.ThrowIfNull(matchField);
            _rules.Add(new CollectionRule<TRecord, TRef, TItem>(
                record, collectionSelector, itemValueSelector, matchField));
            return this;
        }

        public async Task<OsduValidationResult> ExecuteAsync(CancellationToken ct = default)
        {
            if (_rules.Count == 0)
                return OsduValidationResult.Success();

            // Phase 1: Load caches — one fetch per OSDU type, concurrent across types
            var sharedCache = new Dictionary<Type, HashSet<string>>();
            var typesToLoad = _rules.Select(r => r.ReferenceType).Distinct().ToList();

            await Task.WhenAll(typesToLoad.Select(type =>
                _rules.First(r => r.ReferenceType == type)
                      .EnsureCacheLoadedAsync(sharedCache, cacheProvider, ct)));

            logger.LogDebug("Loaded {TypeCount} cache(s), executing {RuleCount} rule(s)", typesToLoad.Count, _rules.Count);

            // Phase 2: Execute all rules synchronously — no I/O, data already loaded
            var combined = OsduValidationResult.Combine(_rules.Select(r => r.Execute(sharedCache)));

            if (!combined.IsValid)
                logger.LogWarning("Validation failed with {ErrorCount} error(s): {Result}", combined.Errors.Count, combined);

            return combined;
        }
    }
}