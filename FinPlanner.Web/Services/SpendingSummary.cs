using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Web.Services;

/// <summary>Keeps calculated plans and spending results available across pages in this browser session.</summary>
public sealed class SpendingSummary : IDisposable
{
    private readonly Scenario scenario;
    private readonly PlanCalculator calculator;
    private readonly Plan?[] plans = new Plan?[3];
    private readonly string?[] errors = new string?[3];
    private string? calculatedSnapshot;

    public SpendingSummary(Scenario scenario, PlanCalculator calculator)
    {
        this.scenario = scenario;
        this.calculator = calculator;
        scenario.Changed += Calculate;
    }

    public Plan? FinancialPlan => plans[0];
    public Plan? MaximumPlan => plans[1];
    public Plan? PerpetualPlan => plans[2];
    public string? FinancialError => errors[0];
    public string? MaximumError => errors[1];
    public string? PerpetualError => errors[2];
    public decimal? MaximumSpend => FirstYearExpenses(MaximumPlan);
    public decimal? PerpetualSpend => FirstYearExpenses(PerpetualPlan);
    public event Action? Changed;

    // Scenario loading and committed edits notify once all input changes are applied.
    // Ignore notifications from saving or cancelling an edit that changed no inputs.
    public void Calculate()
    {
        var snapshot = JsonSerializer.Serialize(new { Scenario = scenario, scenario.StartYear, scenario.TaxInfo });
        if (snapshot == calculatedSnapshot) return;

        Array.Clear(plans);
        Array.Clear(errors);
        try
        {
            if (scenario.Accounts.Count == 0) throw new InvalidOperationException("Add at least one account in your scenario before calculating a plan.");
            if (scenario.LifeExpectancy <= scenario.CurrentAge) throw new InvalidOperationException("Life expectancy must be greater than your current age.");
            for (var index = 0; index < plans.Length; index++)
            {
                try
                {
                    plans[index] = index switch
                    {
                        1 => calculator.CalculateMaximumSpend(scenario),
                        2 => calculator.CalculatePerpetualSpend(scenario),
                        _ => calculator.CalculateCurrentTrajectory(scenario)
                    };
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
                { errors[index] = ex.Message; }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { Array.Fill(errors, ex.Message); }

        calculatedSnapshot = snapshot;
        Changed?.Invoke();
    }

    private static decimal? FirstYearExpenses(Plan? plan)
        => plan is { IsSuccessful: true } ? plan.PlanYears.FirstOrDefault()?.TotalExpenses : null;

    public void Dispose() => scenario.Changed -= Calculate;
}
