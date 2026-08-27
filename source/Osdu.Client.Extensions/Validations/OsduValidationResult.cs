namespace Osdu.Client.Extensions.Validations;

/// <summary>
/// The result of one or more OSDU resource validation operations.
/// </summary>
public sealed class OsduValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<OsduValidationError> Errors { get; init; } = [];

    public static OsduValidationResult Success() => new();

    public static OsduValidationResult Failure(params OsduValidationError[] errors) =>
        new() { Errors = [.. errors] };

    public static OsduValidationResult Combine(IEnumerable<OsduValidationResult> results) => new() { Errors = results.SelectMany(r => r.Errors).ToList() };

    /// <summary>
    /// Throws <see cref="OsduValidationException"/> if any errors exist.
    /// </summary>
    public OsduValidationResult ThrowIfInvalid()
    {
        if (!IsValid)
            throw new OsduValidationException(this);
        return this;
    }

    public override string ToString() => IsValid 
        ? "Validation passed."
        : $"Validation failed with {Errors.Count} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, Errors)}";
}

/// <summary>
/// Describes a single validation failure.
/// </summary>
public sealed class OsduValidationError
{
    /// <summary>The value that was not found.</summary>
    public required string? Value { get; init; }

    /// <summary>The OSDU type that was searched.</summary>
    public required string ReferenceType { get; init; }

    /// <summary>The field name on the source record.</summary>
    public required string FieldName { get; init; }

    public override string ToString() =>
        $"'{Value}' not found in '{ReferenceType}' (field: '{FieldName}').";
}