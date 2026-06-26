using System.Text.Json.Serialization;

namespace FinPlanner.Data;

public class Expense
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int YearStart { get; set; }
    public int YearEnd { get; set; }
    public decimal AnnualRateOfIncrease { get; set; }

    [JsonPropertyName("annualRateOfIncrease")]
    public decimal LegacyAnnualRateOfIncrease
    {
        set => AnnualRateOfIncrease = value;
    }
    public DateTime LastUpdated { get; set; }
}
