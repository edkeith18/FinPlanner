namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{

    /// <summary>
    /// The calendar year represented by this PlanYear at the end of the 12-month period.
    /// </summary>
    public int EndingYear { get; init; }

    /// <summary>
    /// The user's age at the end of the 12-month period.
    /// </summary>
    public int EndingAge { get; init; }

    /// <summary>
    /// The accounts in the plan for this year.
    /// </summary>
    public List<Account> Accounts { get; set; } = new List<Account>();

    public decimal AnnualExpenses { get; set; }

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.EndingBalance);

    public static PlanYear ForecastFrom(PlanYear previousYear)
    {
        return new PlanYear
        {
            EndingAge = previousYear.EndingAge + 1,
            EndingYear = previousYear.EndingYear + 1,
            Accounts = previousYear.Accounts
                .Select(account => new Account(account))
                .ToList()
        };
    }

}
