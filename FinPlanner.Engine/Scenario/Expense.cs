using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Expense
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }

    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }

    public bool UseInflationValue { get; set; } = true;

    public DateTime LastUpdated { get; set; }

    public Expense() { }

    public Expense(Expense sourceExpense)
    {
        Name = sourceExpense.Name;
        Amount = decimal.Round(sourceExpense.Amount, 0, MidpointRounding.AwayFromZero);
        AgeStart = sourceExpense.AgeStart;
        AgeEnd = sourceExpense.AgeEnd;
        AnnualRateOfIncrease = sourceExpense.AnnualRateOfIncrease;
        UseInflationValue = sourceExpense.UseInflationValue;
        LastUpdated = sourceExpense.LastUpdated;
    }

    public void Inflate()
    {
        Amount = decimal.Round(
            Amount * (1 + AnnualRateOfIncrease),
            0,
            MidpointRounding.AwayFromZero);
    }
}
