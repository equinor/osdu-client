namespace Osdu.Client.Extensions.Validations;

/// <summary>
/// Thrown via <see cref="OsduValidationResult.ThrowIfInvalid"/> when validation fails.
/// </summary>
public sealed class OsduValidationException(OsduValidationResult result) : Exception(result.ToString())
{
    /// <summary>The full validation result containing all errors.</summary>
    public OsduValidationResult Result { get; } = result;
}