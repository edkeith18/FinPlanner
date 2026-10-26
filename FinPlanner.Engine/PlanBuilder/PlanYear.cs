namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{

    /// <summary>
    /// The calendar year represented by this PlanYear at the start of the 12-month period.
    /// </summary>
    public int StartingYear { get; init; }

    /// <summary>
    /// The user's age at the start of the 12-month period.
    /// </summary>
    public int StartingAge { get; init; }

    /// <summary>
    /// The calendar year at the end of the 12-month period.
    /// </summary>
    public int EndingYear => StartingYear + 1;

    /// <summary>
    /// The user's age at the end of the 12-month period.
    /// </summary>
    public int EndingAge => StartingAge + 1;

    /// <summary>
    /// The accounts in the plan for this year.
    /// </summary>
    public List<Account> Accounts { get; set; } = new List<Account>();

    /// <summary>
    /// Mandatory and voluntary withdrawals, with one income entry per account.
    /// </summary>
    public List<Income> Incomes { get; private set; } = new();

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
        Expenses.Sum(expense => expense.Amount);

    public static PlanYear FromScenario(
        Scenario scenario,
        decimal uncategorizedExpenseAmount)
    {
        var expenses = scenario.Expenses
            .Select(expense => new Expense(expense))
            .ToList();

        expenses.Add(new Expense
        {
            Name = "Uncategorized Expenses",
            Amount = uncategorizedExpenseAmount,
            AgeStart = scenario.CurrentAge,
            AgeEnd = scenario.LifeExpectancy,
            AnnualRateOfIncrease = scenario.AnnualInflationRate / 100m,
            UseInflationValue = true
        });

        var year = new PlanYear
        {
            StartingAge = scenario.CurrentAge,
            StartingYear = scenario.StartYear,
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
            Expenses = expenses
                .Select(expense =>
                {
                    var newExpense = new Expense(expense);
                    newExpense.PlanStartAge = scenario.CurrentAge;
                    newExpense.ForecastFromAge(scenario.CurrentAge);
                    return newExpense;
                })
                .ToList()
        };
        year.Incomes = year.Accounts.Select(account => new Income(account)).ToList();
        return year;
    }

    public static PlanYear ForecastFrom(PlanYear previousYear)
    {
        var year = new PlanYear
        {
            StartingAge = previousYear.EndingAge,
            StartingYear = previousYear.EndingYear,
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
                    newExpense.ForecastFromAge(previousYear.EndingAge);
                    return newExpense;
                })
                .ToList()
        };
        year.Incomes = year.Accounts.Select(account => new Income(account)).ToList();
        return year;
    }

    public decimal ApplyExpenses()
    {
        var mandatoryIncome = Incomes.Sum(income =>
            Math.Min(income.MandatoryAmount, Math.Max(0m, income.Account.EndingBalance)));
        var surplus = Math.Max(0m, mandatoryIncome - TotalExpenses);
        var destination = surplus > 0m
            ? Accounts.Where(account => account.Type == AccountType.Brokerage)
                .OrderBy(account => account.WithdrawalPriority)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "A Brokerage account is required to receive excess mandatory income.")
            : null;

        // Withdraw from every mandatory source before following withdrawal priority.
        foreach (var income in Incomes)
        {
            income.MandatoryAmount = Math.Min(
                income.MandatoryAmount, Math.Max(0m, income.Account.EndingBalance));
            income.Account.EndingBalance -= income.MandatoryAmount;
        }

        if (destination is not null)
        {
            destination.EndingBalance += surplus;
        }

        var remainingExpenses = Math.Max(0m, TotalExpenses - mandatoryIncome);
        foreach (var income in Incomes.OrderBy(income => income.Account.WithdrawalPriority))
        {
            if (remainingExpenses <= 0)
            {
                break;
            }

            var withdrawal = Math.Min(Math.Max(0m, income.Account.EndingBalance), remainingExpenses);

            income.Account.EndingBalance -= withdrawal;
            income.VoluntaryAmount = withdrawal;
            remainingExpenses -= withdrawal;
        }

        return remainingExpenses;
    }

}
