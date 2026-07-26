namespace FinPlanner.Engine;

/// <summary>
/// Represents the results of a complete financial planning simulation.
///
/// A Plan consists of an ordered collection of PlanYears beginning with
/// the first planning year and continuing through the final planning year.
/// Each PlanYear is calculated sequentially, with the ending state of one
/// year becoming the starting state of the next.
/// </summary>
public sealed class Plan
{
    /// <summary>
    /// Creates a new Plan.
    /// </summary>
    /// <param name="generatedAt">
    /// The date and time when the plan was generated.
    /// </param>
    /// <param name="planYears">
    /// The ordered collection of calculated plan planYears.
    /// </param>
    /// <param name="isSuccessful">
    /// Whether every scheduled plan year was successfully funded.
    /// </param>
    /// <param name="failureReason">
    /// The reason an unsuccessful plan stopped early.
    /// </param>
    public Plan(
        IReadOnlyList<PlanYear> planYears,
        bool isSuccessful = true,
        string? failureReason = null)
    {
        ArgumentNullException.ThrowIfNull(planYears);

        PlanYears = planYears;
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
    }
    
    /// <summary>
    /// The ordered collection of calculated plan planYears.
    /// </summary>
    public IReadOnlyList<PlanYear> PlanYears { get; }

    /// <summary>
    /// Indicates whether every scheduled year could be funded and calculated.
    /// </summary>
    public bool IsSuccessful { get; }

    /// <summary>
    /// Describes why calculation stopped early when the plan was unsuccessful.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// The final year of the plan, or null if the plan contains no planYears.
    /// </summary>
    public PlanYear? LastYear =>
        PlanYears.LastOrDefault();

    /// <summary>
    /// Returns the PlanYear for the specified calendar year,
    /// or null if the year is not present in the plan.
    /// </summary>
    /// <param name="calendarYear">
    /// The calendar year to retrieve.
    /// </param>
    public PlanYear? GetYear(int calendarYear)
    {
        return PlanYears.FirstOrDefault(
            year => year.CalendarYear == calendarYear);
    }
}
