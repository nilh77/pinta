using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Pinta.SharedKernel.Http;

/// <summary>
/// Request text loses its surrounding spaces before it is validated (docs/screens.md, «Formularios»): people paste
/// emails, NIFs and references with stray blanks. Passwords, tokens and secrets stay exactly as sent, since a space
/// may be part of them. Applied by <see cref="ValidationFilter{T}"/> to every validated request body, so the JSON
/// contract (and the OpenAPI document) stays the same.
/// </summary>
public static class TrimmedStrings
{
    private const int MaxDepth = 4;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    /// <summary>Properties whose value must not change: passwords, tokens and secrets.</summary>
    public static bool IsSecret(string propertyName) =>
        propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trims the string properties of <paramref name="value"/> and of the objects and lists inside it.</summary>
    public static void Trim(object? value) => Trim(value, 0);

    private static void Trim(object? value, int depth)
    {
        if (value is null || depth > MaxDepth || value is string || value.GetType().IsValueType)
        {
            return;
        }

        if (value is IEnumerable items)
        {
            foreach (var item in items)
            {
                Trim(item, depth + 1);
            }

            return;
        }

        foreach (var property in Properties.GetOrAdd(value.GetType(), Readable))
        {
            var current = property.GetValue(value);
            if (property.PropertyType == typeof(string))
            {
                if (current is string text && property.CanWrite && !IsSecret(property.Name) && text.Trim() is var trimmed && trimmed.Length != text.Length)
                {
                    property.SetValue(value, trimmed);
                }
            }
            else
            {
                Trim(current, depth + 1);
            }
        }
    }

    private static PropertyInfo[] Readable(Type type) =>
        type.Namespace?.StartsWith("Pinta", StringComparison.Ordinal) == true
            ? [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)]
            : [];
}
