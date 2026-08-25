#nullable enable

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace Osdu.Client.Validation;

/// <summary>
/// Validates API request parameters before sending HTTP requests.
/// </summary>
internal static class RequestValidator
{
    private static readonly ConcurrentDictionary<Type, CachedTypeInfo> _typeInfoCache = new();

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

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { instance };
        ValidateNestedProperties(instance, parameterName, visited);
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

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < items.Count; i++)
        {
            var context = new ValidationContext(items[i]);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(items[i], context, results, validateAllProperties: true))
            {
                var errors = string.Join("; ", results.ConvertAll(r => r.ErrorMessage));
                throw new ArgumentException($"Request validation failed for item at index {i}: {errors}", parameterName);
            }

            visited.Add(items[i]);
            ValidateNestedProperties(items[i], parameterName, visited, i);
        }
    }

    /// <summary>
    /// Recursively validates nested complex properties that have DataAnnotation attributes.
    /// Uses cached compiled accessors and pre-filtered properties for fast traversal.
    /// Tracks visited objects to prevent infinite recursion from circular references.
    /// </summary>
    private static void ValidateNestedProperties(object instance, string parameterName, HashSet<object> visited, int? itemIndex = null)
    {
        var typeInfo = GetCachedTypeInfo(instance.GetType());

        // Only iterate properties that are known to hold validatable nested objects
        foreach (var accessor in typeInfo.NestedPropertyAccessors)
        {
            var value = accessor.GetValue(instance);
            if (value is null)
                continue;

            var runtimeType = value.GetType();

            // Skip simple types that may appear behind loosely-typed properties (e.g., object, dynamic)
            if (runtimeType.IsPrimitive || runtimeType.IsEnum || runtimeType.IsValueType || value is string)
                continue;

            // Handle lists of complex objects
            if (value is System.Collections.IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var element = list[i];
                    if (element is null)
                        continue;

                    var elementType = element.GetType();
                    if (elementType.IsPrimitive || elementType.IsEnum || elementType.IsValueType || element is string)
                        continue;

                    if (!visited.Add(element))
                        continue;

                    ValidateSingleNested(element, parameterName, accessor.Name, visited, itemIndex);
                }
                continue;
            }

            // Skip already-visited objects to prevent circular reference loops
            if (!visited.Add(value))
                continue;

            ValidateSingleNested(value, parameterName, accessor.Name, visited, itemIndex);
        }
    }

    private static void ValidateSingleNested(object instance, string parameterName, string propertyName, HashSet<object> visited, int? itemIndex)
    {
        var typeInfo = GetCachedTypeInfo(instance.GetType());

        if (!typeInfo.HasValidationAttributes)
            return;

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
        ValidateNestedProperties(instance, parameterName, visited, itemIndex);
    }

    private static CachedTypeInfo GetCachedTypeInfo(Type type)
    {
        return _typeInfoCache.GetOrAdd(type, static t =>
        {
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            bool hasValidation = properties
                .Any(p => p.GetCustomAttributes(typeof(ValidationAttribute), true).Length > 0);

            // Pre-filter to only properties whose runtime type could contain validatable nested objects
            var nestedAccessors = properties
                .Where(IsNestedValidatableProperty)
                .Select(p => new CachedPropertyAccessor(p.Name, BuildCompiledGetter(t, p)))
                .ToArray();

            return new CachedTypeInfo(hasValidation, nestedAccessors);
        });
    }

    /// <summary>
    /// Determines at cache-build time whether a property could hold a validatable nested object.
    /// Excludes primitives, value types, strings, and other simple types.
    /// Properties typed as object/interface are included since the runtime type may be validatable.
    /// </summary>
    private static bool IsNestedValidatableProperty(PropertyInfo property)
    {
        var propType = property.PropertyType;

        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(propType);
        if (underlying is not null)
            propType = underlying;

        // Skip simple types
        if (propType.IsPrimitive || propType.IsEnum || propType.IsValueType || propType == typeof(string))
            return false;

        // Include loosely-typed properties — runtime type may have validation attributes
        if (propType == typeof(object) || propType.IsInterface || propType.IsAbstract)
            return true;

        // Include IList properties (may contain complex objects)
        if (typeof(System.Collections.IList).IsAssignableFrom(propType))
            return true;

        // Include concrete classes that have validation attributes on their own properties
        if (propType.IsClass)
        {
            return propType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.GetCustomAttributes(typeof(ValidationAttribute), true).Length > 0);
        }

        return false;
    }

    /// <summary>
    /// Compiles a lambda expression into a fast property getter delegate,
    /// replacing slow reflection-based GetValue calls.
    /// </summary>
    private static Func<object, object?> BuildCompiledGetter(Type ownerType, PropertyInfo property)
    {
        // (object instance) => (object?)((OwnerType)instance).Property
        var param = Expression.Parameter(typeof(object), "instance");
        var cast = Expression.Convert(param, ownerType);
        var access = Expression.Property(cast, property);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
    }

    /// <summary>
    /// Cached metadata for a type, including pre-filtered property accessors.
    /// </summary>
    private sealed record CachedTypeInfo(bool HasValidationAttributes, CachedPropertyAccessor[] NestedPropertyAccessors);

    /// <summary>
    /// A compiled property accessor with the property name for error reporting.
    /// </summary>
    private sealed class CachedPropertyAccessor(string name, Func<object, object?> getter)
    {
        public string Name { get; } = name;
        public object? GetValue(object instance) => getter(instance);
    }
}