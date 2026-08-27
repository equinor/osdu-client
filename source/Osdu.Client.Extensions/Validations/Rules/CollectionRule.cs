using Osdu.Client.Extensions.Caching;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Osdu.Client.Extensions.Validations.Rules
{
    internal sealed class CollectionRule<TRecord, TRef, TItem> : IValidationRule
    {
        private readonly TRecord _record;
        private readonly Func<TRecord, IEnumerable<TItem>?> _collectionSelector;
        private readonly Func<TItem, string?> _itemValueSelector;
        private readonly Func<TRef, string?> _matchFieldSelector;
        private readonly string _fieldName;

        public CollectionRule(TRecord record, Expression<Func<TRecord, IEnumerable<TItem>?>> collectionSelector, Expression<Func<TItem, string?>> itemValueSelector, Expression<Func<TRef, string?>> matchField)
        {
            _record = record;
            _collectionSelector = OsduDataValidator.CompileAndCache(collectionSelector);
            _itemValueSelector = OsduDataValidator.CompileAndCache(itemValueSelector);
            _matchFieldSelector = OsduDataValidator.CompileAndCache(matchField);
            _fieldName = $"{OsduDataValidator.GetFieldName(collectionSelector)}.{OsduDataValidator.GetFieldName(itemValueSelector)}";
        }

        public Type ReferenceType => typeof(TRef);

        public async Task EnsureCacheLoadedAsync(Dictionary<Type, HashSet<string>> cache, IOsduCacheProvider provider, CancellationToken ct)
        {
            if (cache.ContainsKey(typeof(TRef))) return;

            var items = await provider.GetAllAsync<TRef>(ct);
            cache[typeof(TRef)] = new HashSet<string>(
                items.Select(_matchFieldSelector).Where(v => v is not null)!,
                StringComparer.OrdinalIgnoreCase);
        }

        public OsduValidationResult Execute(Dictionary<Type, HashSet<string>> cache)
        {
            var collection = _collectionSelector(_record);
            if (collection is null)
                return OsduValidationResult.Success();

            var knownValues = cache[typeof(TRef)];

            var errors = collection
                .Select(_itemValueSelector)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(v => !knownValues.Contains(v!))
                .Select(v => new OsduValidationError
                {
                    Value = v,
                    ReferenceType = typeof(TRef).Name,
                    FieldName = _fieldName
                })
                .ToList();

            return errors.Count == 0
                ? OsduValidationResult.Success()
                : new OsduValidationResult { Errors = errors };
        }
    }
}
