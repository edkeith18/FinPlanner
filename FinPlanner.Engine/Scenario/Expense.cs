using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Expense
{
    public ExpenseType Type { get; set; } = ExpenseType.General;
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }

    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }

    public bool UseInflationValue { get; set; } = true;

    public DateTime LastUpdated { get; set; }

    /// <summary>Creates an expense with default values for scenario entry or deserialization.</summary>
    public Expense() { }

    /// <summary>Expense age boundaries are inclusive and use the period's starting age.</summary>
    public bool IsActiveAtAge(int age) => age >= AgeStart && age <= AgeEnd;

    /// <summary>Copies the amount, age boundaries, growth settings, and metadata from an existing expense.</summary>
    public Expense(Expense sourceExpense)
    {
        Name = sourceExpense.Name;
        Type = sourceExpense.Type;
        Amount = sourceExpense.Amount;
        AgeStart = sourceExpense.AgeStart;
        AgeEnd = sourceExpense.AgeEnd;
        AnnualRateOfIncrease = sourceExpense.AnnualRateOfIncrease;
        UseInflationValue = sourceExpense.UseInflationValue;
        LastUpdated = sourceExpense.LastUpdated;
    }
}
