using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

/// <summary>A priority-funded contribution to the account identified by Name.</summary>
public class ScheduledTransfer
{
    public string Name { get; set; } = "";
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal Amount { get; set; }
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    /// <summary>Creates a scheduled transfer with default values and inflation-based growth enabled.</summary>
    public ScheduledTransfer() { }

    /// <summary>Copies the destination name, amount, age boundaries, and growth settings from an existing transfer.</summary>
    public ScheduledTransfer(ScheduledTransfer source)
    {
        Name = source.Name;
        AgeStart = source.AgeStart;
        AgeEnd = source.AgeEnd;
        Amount = source.Amount;
        AnnualRateOfIncrease = source.AnnualRateOfIncrease;
        UseInflationValue = source.UseInflationValue;
    }
}
