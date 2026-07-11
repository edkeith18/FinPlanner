namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single calendar year.
///
/// A PlanYear is immutable once created and contains the calculated
/// state of the user's finances at the end of that year.
///
/// The sequence of PlanYears forms a Plan, where each year's ending
/// state becomes the starting point for the following year.
/// </summary>
public sealed class PlanYear
{
    /// <summary>
    /// The calendar year represented by this PlanYear.
    /// </summary>
    public required int CalendarYear { get; init; }

    /// <summary>
    /// The user's age on December 31 of this calendar year.
    /// </summary>
    public required int Age { get; init; }

    /// <summary>
    /// The calculated results for every account during this year.
    /// </summary>
    public required IReadOnlyList<AccountYearResult> Accounts { get; init; }

    /// <summary>
    /// The calculated federal and state taxes for this year.
    /// </summary>
    public required TaxYearResult Taxes { get; init; }

    /// <summary>
    /// Total balance across all accounts at the beginning of the year.
    /// </summary>
    public decimal BeginningBalance =>
        Accounts.Sum(account => account.BeginningBalance);

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.EndingBalance);
}