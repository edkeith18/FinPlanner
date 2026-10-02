namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{
    public static PlanYear AddFirst(Scenario scenario)
    {
        PlanYear planYear = new PlanYear();

        planYear.AgeAtStart = scenario.CurrentAge;
        planYear.YearAtStart = scenario.StartYear;
        planYear.Accounts = scenario.Accounts.Select(account => new Account(account)).ToList();

        return planYear;
    }

    /// <summary>
    /// The calendar year represented by this PlanYear at the start of the 12-month period.
    /// </summary>
    public int YearAtStart { get; private set; }

    /// <summary>
    /// The user's age at the start of the 12-month period.
    /// </summary>
    public int AgeAtStart { get; private set; }

    /// <summary>
    /// The calculated results for every account during this year.
    /// </summary>
    // Ensure non-nullable property is initialized
    public List<Account> Accounts { get; set; } = new List<Account>();

    /// <summary>
    /// The calculated federal and state taxes for this year.
    /// </summary>
    public TaxYearResult Taxes { get; init; } = new TaxYearResult();

    /// <summary>
    /// Named expenses incurred during this year.
    /// </summary>
    public IReadOnlyList<ExpenseYearResult> Expenses { get; init; } = new List<ExpenseYearResult>();

    public decimal TotalExpenses =>
        Expenses.Sum(expense => expense.Amount);

    /// <summary>
    /// Total balance across all accounts at the beginning of the year.
    /// </summary>
    public decimal BeginningBalance =>
        Accounts.Sum(account => account.Balance);

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.Balance);
}
