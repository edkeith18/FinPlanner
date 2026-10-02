namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{
    public static PlanYear CalculateFirst(
        int startAge,
        int startYear,
        List<Account> accounts)
    {
        return new PlanYear
        {
            BeginningAge = startAge,
            BeginningYear = startYear,
            Accounts = accounts
                .Select(account => new Account(account))
                .ToList()
        };
    }

    public static PlanYear CalculateNext(PlanYear previousYear)
    {
        return new PlanYear
        {
            BeginningAge = previousYear.BeginningAge + 1,
            BeginningYear = previousYear.BeginningYear + 1,
            Accounts = previousYear.Accounts
                .Select(account => new Account(account))
                .ToList()
        };
    }

    /// <summary>
    /// The calendar year represented by this PlanYear at the start of the 12-month period.
    /// </summary>
    public int BeginningYear { get; init; }

    /// <summary>
    /// The user's age at the start of the 12-month period.
    /// </summary>
    public int BeginningAge { get; init; }

    /// <summary>
    /// The accounts in the plan for this year.
    /// </summary>
    public List<Account> Accounts { get; set; } = new List<Account>();

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
        Accounts.Sum(account => account.BeginningBalance);

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.EndingBalance);
}
