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
    /// <param name="years">
    /// The ordered collection of calculated plan years.
    /// </param>
    public Plan(
        DateTimeOffset generatedAt,
        IReadOnlyList<PlanYear> years)
    {
        ArgumentNullException.ThrowIfNull(years);

        GeneratedAt = generatedAt;
        Years = years;
    }

    /// <summary>
    /// The date and time when this plan was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>
    /// The ordered collection of calculated plan years.
    /// </summary>
    public IReadOnlyList<PlanYear> Years { get; }

    /// <summary>
    /// The first year of the plan, or null if the plan contains no years.
    /// </summary>
    public PlanYear? FirstYear =>
        Years.FirstOrDefault();

    /// <summary>
    /// The final year of the plan, or null if the plan contains no years.
    /// </summary>
    public PlanYear? LastYear =>
        Years.LastOrDefault();

    /// <summary>
    /// Returns the PlanYear for the specified calendar year,
    /// or null if the year is not present in the plan.
    /// </summary>
    /// <param name="calendarYear">
    /// The calendar year to retrieve.
    /// </param>
    public PlanYear? GetYear(int calendarYear)
    {
        return Years.FirstOrDefault(
            year => year.CalendarYear == calendarYear);
    }
}