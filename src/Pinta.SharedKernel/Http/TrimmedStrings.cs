using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pinta.SharedKernel.Http;

/// <summary>
/// Request text arrives without surrounding spaces (docs/screens.md, «Formularios»): people paste emails, NIFs and
/// references with stray blanks. Passwords and tokens are left exactly as sent, since a space may be part of them.
/// </summary>
public static class TrimmedStrings
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.Converters.Add(new TrimmingConverter());
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(KeepSecretsAsSent);
    }

    /// <summary>Properties whose value must not change: passwords, tokens and secrets.</summary>
    public static bool IsSecret(string propertyName) =>
        propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static void KeepSecretsAsSent(JsonTypeInfo type)
    {
        if (type.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in type.Properties)
        {
            if (property.PropertyType == typeof(string) && IsSecret(property.Name))
            {
                property.CustomConverter = AsSentConverter.Instance;
            }
        }
    }

    private sealed class TrimmingConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString()?.Trim();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    private sealed class AsSentConverter : JsonConverter<string>
    {
        public static readonly AsSentConverter Instance = new();

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}
