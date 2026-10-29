namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{
    private List<Transfer> scheduledTransfers = new();
    private int planStartAge;
    private decimal inflationRate;
    public decimal UnfundedTransfers { get; private set; }


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
    /// Earned investment income, with one entry per account.
    /// </summary>
    public List<AccountEarnings> AccountEarnings { get; private set; } = new();

    /// <summary>Individual scheduled transfers, RMDs, expense withdrawals, and cash reinvestments.</summary>
    public List<AccountTransfer> Transfers { get; private set; } = new();

    public decimal MandatoryCashFlow => AccountEarnings.Sum(income => income.Amount)
        + Transfers.Sum(transfer => transfer.MandatoryAmount);

    public decimal TotalCashFlow => AccountEarnings.Sum(income => income.Amount)
        + Transfers.Where(transfer => transfer.ToAccount is null).Sum(transfer => transfer.Amount);

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
            scheduledTransfers = scenario.Transfers.Select(transfer => new Transfer(transfer)).ToList(),
            planStartAge = scenario.CurrentAge,
            inflationRate = scenario.AnnualInflationRate / 100m,
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
        year.AccountEarnings = year.Accounts.Select(account => new AccountEarnings(account)).ToList();
        year.BuildTransfers();
        return year;
    }

    public static PlanYear ForecastFrom(PlanYear previousYear)
    {
        var year = new PlanYear
        {
            scheduledTransfers = previousYear.scheduledTransfers.Select(transfer => new Transfer(transfer)).ToList(),
            planStartAge = previousYear.planStartAge,
            inflationRate = previousYear.inflationRate,
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
        year.AccountEarnings = year.Accounts.Select(account => new AccountEarnings(account)).ToList();
        year.BuildTransfers();
        return year;
    }

    private void BuildTransfers()
    {
        Transfers = Accounts.Select(account => new AccountTransfer(account, StartingAge))
            .Where(transfer => transfer.RequestedAmount > 0m).ToList();
        foreach (var instruction in scheduledTransfers)
        {
            var amount = instruction.ForecastAmount(planStartAge, StartingAge, inflationRate);
            if (amount <= 0m) continue;
            var from = FindTransferAccount(instruction.FromAccountName);
            var to = FindTransferAccount(instruction.ToAccountName);
            Transfers.Add(new AccountTransfer(instruction.Name, from, to, amount, TransferReason.Scheduled));
        }
    }

    private Account FindTransferAccount(string name)
    {
        var matches = Accounts.Where(account => account.Name == name).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"Transfer account '{name}' must identify exactly one account.");
        return matches[0];
    }

    public decimal ApplyExpenses()
    {
        var earned = AccountEarnings.Sum(income => income.Amount);
        var required = Transfers.Where(transfer => transfer.Reason == TransferReason.Rmd)
            .Sum(transfer => Math.Min(transfer.RequestedAmount,
                Math.Max(0m, transfer.FromAccount!.EndingBalance)));
        var surplus = Math.Max(0m, earned + required - TotalExpenses);
        var destination = surplus > 0m
            ? Accounts.Where(account => account.Type == AccountType.Brokerage)
                .OrderBy(account => account.WithdrawalPriority).FirstOrDefault()
                ?? throw new InvalidOperationException("A Brokerage account is required to receive excess mandatory income.")
            : null;

        // Earnings are new cash in addition to appreciation. RMDs instead
        // withdraw existing funds into that same cash pool.
        var cash = earned;
        foreach (var transfer in Transfers.Where(transfer => transfer.Reason == TransferReason.Rmd))
            cash += transfer.Execute();

        // Explicit account-to-account instructions are separate from expense
        // funding. Required distributions take precedence over these movements.
        foreach (var transfer in Transfers.Where(transfer => transfer.Reason == TransferReason.Scheduled))
        {
            transfer.Execute();
            UnfundedTransfers += transfer.RequestedAmount - transfer.Amount;
        }

        if (destination is not null)
        {
            var reinvestment = new AccountTransfer("Unspent cash", null, destination,
                surplus, TransferReason.SurplusReinvestment);
            reinvestment.Execute();
            Transfers.Add(reinvestment);
        }

        var remainingExpenses = Math.Max(0m, TotalExpenses - cash);
        foreach (var account in Accounts.OrderBy(account => account.WithdrawalPriority))
        {
            if (remainingExpenses <= 0m) break;
            var amount = Math.Min(Math.Max(0m, account.EndingBalance), remainingExpenses);
            if (amount <= 0m) continue;
            var withdrawal = new AccountTransfer("Expense funding", account, null,
                amount, TransferReason.ExpenseWithdrawal);
            remainingExpenses -= withdrawal.Execute();
            Transfers.Add(withdrawal);
        }
        return remainingExpenses + UnfundedTransfers;
    }
}
