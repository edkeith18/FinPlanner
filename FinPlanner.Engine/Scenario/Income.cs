using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Income
{
    public string Name { get; set; } = "";
    public decimal InitialAmount { get; set; }
    public IncomeType Type { get; set; } = IncomeType.Ordinary;
    public int AgeStart { get; set; }
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }
    public DateTime LastUpdated { get; set; }
}
