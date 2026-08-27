using System.Linq.Expressions;

namespace Osdu.Client.Extensions.Validations;

/// <summary>
/// Validates that OSDU resource references exist in the platform by looking them up
/// through the cache layer. Works with any cached OSDU type — reference-data (UnitOfMeasure,
/// LogType, ExistenceKind, etc.) and master-data (Well, Wellbore, Organisation, etc.).
/// </summary>
public interface IOsduDataValidator
{
    /// <summary>
    /// Creates a fluent validation builder for a record of any type.
    /// Chain <c>.Validate()</c> calls, then call <c>.ExecuteAsync()</c>.
    /// Rules targeting the same OSDU type share a single cache fetch.
    /// </summary>
    IValidationBuilder<TRecord> For<TRecord>(TRecord record);

    /// <summary>
    /// Fluent builder for composing validation rules against a single record.
    /// </summary>
    interface IValidationBuilder<TRecord>
    {
        /// <summary>
        /// Validates that the field value exists in the cache for <typeparamref name="TRef"/>.
        /// </summary>
        IValidationBuilder<TRecord> Validate<TRef>(Expression<Func<TRecord, string?>> valueSelector, Expression<Func<TRef, string?>> matchField);

        /// <summary>
        /// Validates that every value in the collection exists in the cache for <typeparamref name="TRef"/>.
        /// Field name is derived as <c>"{collection}.{itemField}"</c>.
        /// </summary>
        IValidationBuilder<TRecord> ValidateAll<TRef, TItem>(Expression<Func<TRecord, IEnumerable<TItem>?>> collectionSelector, Expression<Func<TItem, string?>> itemValueSelector, Expression<Func<TRef, string?>> matchField);

        /// <summary>
        /// Executes all rules and returns a composite result.
        /// Rules targeting the same OSDU type share a single cache lookup.
        /// </summary>
        Task<OsduValidationResult> ExecuteAsync(CancellationToken ct = default);
    }
}