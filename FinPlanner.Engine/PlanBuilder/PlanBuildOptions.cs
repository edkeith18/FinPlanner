namespace FinPlanner.Engine;

/// <summary>
/// Optional assumptions that apply to a single plan calculation.
/// </summary>
public sealed record PlanBuildOptions
{
    /// <summary>
    /// Overrides the scenario's annual-expense target without modifying the scenario.
    /// Useful for calculating maximum annual expenses.
    /// </summary>
    public decimal? AnnualExpenses { get; init; }
}
