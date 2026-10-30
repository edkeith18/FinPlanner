using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

/// <summary>
/// Reads percentage values from scenario JSON and stores them internally as decimal rates.
/// For example, 7 in JSON becomes 0.07m in the engine.
/// </summary>
public sealed class PercentageJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.GetDecimal() / 100m;
    }

    public override void Write(
        Utf8JsonWriter writer,
        decimal value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value * 100m);
    }
}
