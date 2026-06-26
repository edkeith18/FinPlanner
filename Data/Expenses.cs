using System.Text.Json.Serialization;

namespace FinPlanner.Data;

public class Expense
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    [JsonPropertyName("YearStart")]
    public int LegacyYearStart
    {
        set => AgeStart = value;
    }

    [JsonPropertyName("YearEnd")]
    public int LegacyYearEnd
    {
        set => AgeEnd = value;
    }

    [JsonPropertyName("annualRateOfIncrease")]
    public decimal LegacyAnnualRateOfIncrease
    {
        set => AnnualRateOfIncrease = value;
    }
    public DateTime LastUpdated { get; set; }
}
