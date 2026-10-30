using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

/// <summary>
/// Reads supported date-of-birth input formats and writes a stable full-year
/// representation for scenario JSON.
/// </summary>
public sealed class DateOfBirthJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("DateOfBirth must be a string in MM/dd/yy or MM/dd/yyyy format.");
        }

        var value = reader.GetString();
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (!Scenario.TryParseDateOfBirth(value, today, out var dateOfBirth, out var validationMessage))
        {
            throw new JsonException($"Invalid DateOfBirth: {validationMessage}");
        }

        return dateOfBirth;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
    }
}
