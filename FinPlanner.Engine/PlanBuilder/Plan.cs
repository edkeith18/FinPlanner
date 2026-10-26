namespace FinPlanner.Engine;

/// <summary>
/// The result of calculating a financial plan.
/// </summary>
public class Plan
{
    public string Name { get; }
    public int BeginningYear { get; }
    public int EndingYear { get; }
    public decimal AnnualExpenses { get; }
    public bool IsSuccessful { get; }
    public string? FailureReason { get; }

    /// <summary>
    /// The calculated 12-month periods, in chronological order.
    /// An unsuccessful forecast can contain the periods calculated before failure.
    /// </summary>
    public IReadOnlyList<PlanYear> PlanYears { get; }

    internal Plan(Scenario scenario, string name, List<PlanYear> years,
        bool isSuccessful, string? failureReason = null)
    {
        Name = name;
        BeginningYear = scenario.StartYear;
        EndingYear = scenario.EndYear;
        AnnualExpenses = scenario.AnnualExpenses;
        PlanYears = years.AsReadOnly();
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
    }
}
