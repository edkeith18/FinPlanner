using FinPlanner.Engine;

namespace FinPlanner.Web.Services;

/// <summary>Keeps spending results available across pages in this browser session.</summary>
public sealed class SpendingSummary : IDisposable
{
    private readonly Scenario scenario;

    public SpendingSummary(Scenario scenario)
    {
        this.scenario = scenario;
        scenario.Changed += Clear;
    }

    public decimal? MaximumSpend { get; private set; }
    public decimal? PerpetualSpend { get; private set; }
    public event Action? Changed;

    public void Update(Plan? maximum, Plan? perpetual)
    {
        MaximumSpend = FirstYearExpenses(maximum);
        PerpetualSpend = FirstYearExpenses(perpetual);
        Changed?.Invoke();
    }

    public void Clear() => Update(null, null);

    private static decimal? FirstYearExpenses(Plan? plan)
        => plan is { IsSuccessful: true } ? plan.PlanYears.FirstOrDefault()?.TotalExpenses : null;

    public void Dispose() => scenario.Changed -= Clear;
}
