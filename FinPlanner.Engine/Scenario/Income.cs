using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Income
{
    public string Name { get; set; } = "";
    public decimal InitialAmount { get; set; }
    // Retain the existing UI/API property and accept legacy Amount JSON.
    [JsonIgnore]
    public decimal Amount { get => InitialAmount; set => InitialAmount = value; }
    [JsonPropertyName("Amount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LegacyAmount { get => null; set { if (value.HasValue) InitialAmount = value.Value; } }
    public int AgeStart { get; set; }
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }
    public DateTime LastUpdated { get; set; }
}
