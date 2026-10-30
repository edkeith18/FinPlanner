using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

/// <summary>A scheduled instruction forecast into account movements for each active plan year.</summary>
public class ScheduledTransfer
{
    public string Name { get; set; } = "";
    public string FromAccountName { get; set; } = "";
    public string ToAccountName { get; set; } = "";
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal Amount { get; set; }
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    public ScheduledTransfer() { }

    public ScheduledTransfer(ScheduledTransfer source)
    {
        Name = source.Name;
        FromAccountName = source.FromAccountName;
        ToAccountName = source.ToAccountName;
        AgeStart = source.AgeStart;
        AgeEnd = source.AgeEnd;
        Amount = source.Amount;
        AnnualRateOfIncrease = source.AnnualRateOfIncrease;
        UseInflationValue = source.UseInflationValue;
    }
}
