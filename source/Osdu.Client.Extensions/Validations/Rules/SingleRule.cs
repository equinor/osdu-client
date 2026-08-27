using Osdu.Client.Extensions.Caching;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Osdu.Client.Extensions.Validations.Rules
{
    internal sealed class SingleRule<TRecord, TRef> : IValidationRule
    {
        private readonly TRecord _record;
        private readonly Func<TRecord, string?> _valueSelector;
        private readonly Func<TRef, string?> _matchFieldSelector;
        private readonly string _fieldName;

        public SingleRule(TRecord record, Expression<Func<TRecord, string?>> valueSelector, Expression<Func<TRef, string?>> matchField)
        {
            _record = record;
            _valueSelector = OsduDataValidator.CompileAndCache(valueSelector);
            _matchFieldSelector = OsduDataValidator.CompileAndCache(matchField);
            _fieldName = OsduDataValidator.GetFieldName(valueSelector);
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
            var value = _valueSelector(_record);
            if (string.IsNullOrWhiteSpace(value))
                return OsduValidationResult.Success();

            return cache[typeof(TRef)].Contains(value)
                ? OsduValidationResult.Success()
                : OsduValidationResult.Failure(new OsduValidationError
                {
                    Value = value,
                    ReferenceType = typeof(TRef).Name,
                    FieldName = _fieldName
                });
        }
    }
}
