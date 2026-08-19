using System.Text.Json;
using System.Text.Json.Serialization;

namespace Osdu.Client.Converters;

/// <summary>
/// Handles JSON values that may represent booleans as strings or numbers.
/// </summary>
public class BooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString()?.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                "" or null => null,
                _ => throw new JsonException($"Unable to convert \"{reader.GetString()}\" to boolean.")
            },
            JsonTokenType.Number => reader.TryGetInt64(out long num) ? num != 0 : null,
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} for boolean property.")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteBooleanValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
