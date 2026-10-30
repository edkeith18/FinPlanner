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

    /// <summary>Expense age boundaries are inclusive and use the period's starting age.</summary>
    public bool IsActiveAtAge(int age) => age >= AgeStart && age <= AgeEnd;

    public Expense(Expense sourceExpense)
    {
        Name = sourceExpense.Name;
        Amount = sourceExpense.Amount;
        AgeStart = sourceExpense.AgeStart;
        AgeEnd = sourceExpense.AgeEnd;
        AnnualRateOfIncrease = sourceExpense.AnnualRateOfIncrease;
        UseInflationValue = sourceExpense.UseInflationValue;
        LastUpdated = sourceExpense.LastUpdated;
    }
}
