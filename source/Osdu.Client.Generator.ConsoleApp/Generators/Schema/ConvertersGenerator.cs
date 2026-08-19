using System.Text;
using Microsoft.Extensions.Logging;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Schema;

/// <summary>
/// Generates JSON converter classes needed by the generated schema types.
/// </summary>
public class ConvertersGenerator
{
    private readonly ILogger<ConvertersGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public ConvertersGenerator(ILogger<ConvertersGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates converter files into the Converters directory under the output base.
    /// </summary>
    public void Generate()
    {
        string outputDir = Path.Combine(_configuration.OutputBaseDir, "Converters");
        Directory.CreateDirectory(outputDir);

        GenerateBooleanConverter(outputDir);
        GenerateNullableDateTimeOffsetConverter(outputDir);
    }

    private void GenerateBooleanConverter(string outputDir)
    {
        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);

        sb.AppendLine("""
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
            """);

        string outputFile = Path.Combine(outputDir, "BooleanConverter.cs");
        File.WriteAllText(outputFile, sb.ToString());

        _logger.LogInformation($"Generated BooleanConverter: {outputFile}");
    }

    private void GenerateNullableDateTimeOffsetConverter(string outputDir)
    {
        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);

        sb.AppendLine("""
            using System.Text.Json;
            using System.Text.Json.Serialization;

            namespace Osdu.Client.Converters;

            /// <summary>
            /// Handles JSON values that may represent DateTimeOffset but could be empty strings or invalid formats.
            /// </summary>
            public class NullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
            {
                public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    if (reader.TokenType == JsonTokenType.Null)
                        return null;

                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var value = reader.GetString();
                        if (string.IsNullOrWhiteSpace(value))
                            return null;

                        if (DateTimeOffset.TryParse(value, out var result))
                            return result;

                        return null;
                    }

                    return reader.GetDateTimeOffset();
                }

                public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
                {
                    if (value.HasValue)
                        writer.WriteStringValue(value.Value);
                    else
                        writer.WriteNullValue();
                }
            }
            """);

        string outputFile = Path.Combine(outputDir, "NullableDateTimeOffsetConverter.cs");
        File.WriteAllText(outputFile, sb.ToString());

        _logger.LogInformation($"Generated NullableDateTimeOffsetConverter: {outputFile}");
    }
}
