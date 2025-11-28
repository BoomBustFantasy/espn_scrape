using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESPNScrape.Converters;

public class ESPNNumericConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();

            // Handle common ESPN string values that should be numeric
            if (string.IsNullOrEmpty(stringValue) || stringValue == "-" || stringValue == "N/A")
            {
                return 0.0;
            }

            // Try to parse as double
            if (double.TryParse(stringValue, out var result))
            {
                return result;
            }

            // Handle percentage values like "66.7%" 
            if (stringValue.EndsWith("%") && double.TryParse(stringValue.TrimEnd('%'), out var percentage))
            {
                return percentage;
            }

            // If all else fails, return 0
            return 0.0;
        }

        return 0.0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}