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

    /// <summary>
    /// The expenses in the plan for this year.
    /// </summary>
    public List<Expense> Expenses { get; set; } = new List<Expense>();

    public decimal AnnualExpenses { get; set; }

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.EndingBalance);

    /// <summary>
    /// Total expenses that apply at this age.
    /// </summary>
    public decimal TotalExpenses =>
        Expenses
            .Where(expense =>
                EndingAge >= expense.AgeStart &&
                EndingAge <= expense.AgeEnd)
            .Sum(expense => expense.Amount);

    public static PlanYear FromScenario(Scenario scenario)
    {
        return new PlanYear
        {
            EndingAge = scenario.CurrentAge,
            EndingYear = scenario.StartYear,
            AnnualExpenses = scenario.AnnualExpenses,
            Accounts = scenario.Accounts
                .Select(account =>
                {
                    var newAccount = new Account(account);

                    newAccount.AppreciationRate =
                        account.Holdings == AccountHoldings.Bonds
                            ? scenario.BondsAnnualRateOfReturn
                            : scenario.SecuritiesAnnualRateOfReturn;

                    return newAccount;
                })
                .ToList(),
            Expenses = scenario.Expenses
                .Select(expense => new Expense(expense))
                .ToList()
        };
    }

    public static PlanYear ForecastFrom(PlanYear previousYear)
    {
        return new PlanYear
        {
            EndingAge = previousYear.EndingAge + 1,
            EndingYear = previousYear.EndingYear + 1,
            AnnualExpenses = previousYear.AnnualExpenses,
            Accounts = previousYear.Accounts
                .Select(account =>
                {
                    var newAccount = new Account(account);
                    newAccount.BeginningBalance = decimal.Round(
                        account.EndingBalance,
                        0,
                        MidpointRounding.AwayFromZero);
                    return newAccount;
                })
                .ToList(),
            Expenses = previousYear.Expenses
                .Select(expense =>
                {
                    var newExpense = new Expense(expense);
                    newExpense.Inflate();
                    return newExpense;
                })
                .ToList()
        };
    }

    public decimal ApplyExpenses()
    {
        var remainingExpenses = TotalExpenses;

        foreach (var account in Accounts.OrderBy(account => account.WithdrawalPriority))
        {
            if (remainingExpenses <= 0)
            {
                break;
            }

            var withdrawal = Math.Min(account.EndingBalance, remainingExpenses);

            account.EndingBalance -= withdrawal;
            remainingExpenses -= withdrawal;
        }

        return remainingExpenses;
    }

}
