using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Expense
{
    private decimal? initialAmount;

    [JsonIgnore]
    public int? PlanStartAge { get; set; }

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
        initialAmount = sourceExpense.initialAmount ?? sourceExpense.Amount;
        PlanStartAge = sourceExpense.PlanStartAge;
    }

    /// <summary>
    /// Updates Amount for the requested age, compounded from the original amount.
    /// AgeStart and AgeEnd are inclusive. Rounding occurs only at the target age.
    /// </summary>
    public void ForecastFromAge(int age)
    {
        if (PlanStartAge is not int startAge)
        {
            throw new InvalidOperationException("A plan-start age is required to forecast an expense.");
        }
        if (age < startAge)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "Age cannot precede the plan-start age.");
        }

        initialAmount ??= Amount;
        if (!IsActiveAtAge(age))
        {
            Amount = 0m;
            return;
        }

        var growthFactor = 1m;
        for (var year = startAge; year < age; year++)
        {
            growthFactor *= 1 + AnnualRateOfIncrease;
        }
        Amount = decimal.Round(
            initialAmount.Value * growthFactor,
            0,
            MidpointRounding.AwayFromZero);
    }
}
