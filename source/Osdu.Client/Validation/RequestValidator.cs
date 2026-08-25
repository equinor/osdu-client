#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Osdu.Client.Validation;

/// <summary>
/// Validates API request parameters before sending HTTP requests.
/// </summary>
internal static class RequestValidator
{
    /// <summary>
    /// Validates that a required string parameter is not null or empty.
    /// </summary>
    public static void RequireNotNullOrEmpty(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
    }

    /// <summary>
    /// Validates that a required nullable value type parameter has a value.
    /// </summary>
    public static void RequireNotNull<T>(T? value, string parameterName) where T : struct
    {
        if (!value.HasValue)
        {
            throw new ArgumentNullException(parameterName, $"Required parameter '{parameterName}' must have a value.");
        }
    }

    /// <summary>
    /// Validates that a required reference type parameter is not null.
    /// </summary>
    public static void RequireNotNull<T>(T? value, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
    }

    /// <summary>
    /// Validates that a required list is not null or empty.
    /// Works with any element type: string, int, bool, DateTime, custom objects, etc.
    /// </summary>
    public static void RequireNotNullOrEmptyList<T>(IList<T>? items, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        if (items.Count == 0)
        {
            throw new ArgumentException("List must contain at least one item.", parameterName);
        }
    }

    /// <summary>
    /// Validates a single object against its DataAnnotation attributes.
    /// Checks [Required], [RegularExpression], [Range], [StringLength], etc.
    /// </summary>
    public static void ValidateObject<T>(T instance, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance, parameterName);

        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(instance, context, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.ConvertAll(r => r.ErrorMessage));
            throw new ArgumentException($"Request validation failed: {errors}", parameterName);
        }

        ValidateNestedProperties(instance, parameterName);
    }

    /// <summary>
    /// Validates each item in a list against its DataAnnotation attributes.
    /// Ensures the list is not null or empty, then validates each element.
    /// </summary>
    public static void ValidateObjectList<T>(IList<T> items, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        if (items.Count == 0)
        {
            throw new ArgumentException("List must contain at least one item.", parameterName);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var context = new ValidationContext(items[i]);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(items[i], context, results, validateAllProperties: true))
            {
                var errors = string.Join("; ", results.ConvertAll(r => r.ErrorMessage));
                throw new ArgumentException($"Request validation failed for item at index {i}: {errors}", parameterName);
            }

            ValidateNestedProperties(items[i], parameterName, i);
        }
    }

    /// <summary>
    /// Recursively validates nested complex properties that have DataAnnotation attributes.
    /// </summary>
    private static void ValidateNestedProperties(object instance, string parameterName, int? itemIndex = null)
    {
        var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(instance);
            if (value is null)
                continue;

            var valueType = value.GetType();

            // Skip primitive types, strings, value types, and other non-complex types
            if (valueType.IsPrimitive || valueType.IsEnum || valueType.IsValueType || value is string)
                continue;

            // Handle lists of complex objects
            if (value is System.Collections.IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var element = list[i];
                    if (element is not null && !element.GetType().IsPrimitive && element is not string)
                    {
                        ValidateSingleNested(element, parameterName, property.Name, itemIndex);
                    }
                }
                continue;
            }

            // Validate complex nested objects
            if (valueType.IsClass && HasValidationAttributes(valueType))
            {
                ValidateSingleNested(value, parameterName, property.Name, itemIndex);
            }
        }
    }

    private static void ValidateSingleNested(object instance, string parameterName, string propertyName, int? itemIndex)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(instance, context, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.ConvertAll(r => r.ErrorMessage));
            var prefix = itemIndex.HasValue
                ? $"Request validation failed for item at index {itemIndex.Value}, property '{propertyName}'"
                : $"Request validation failed for property '{propertyName}'";
            throw new ArgumentException($"{prefix}: {errors}", parameterName);
        }

        // Recurse into deeper nested objects
        ValidateNestedProperties(instance, parameterName, itemIndex);
    }

    private static bool HasValidationAttributes(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttributes(typeof(ValidationAttribute), true).Length > 0);
    }
}