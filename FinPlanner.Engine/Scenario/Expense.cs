using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Expense
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    public DateTime LastUpdated { get; set; }
}
