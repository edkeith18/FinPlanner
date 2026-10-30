namespace FinPlanner.Engine;

/// <summary>Creates and calculates annual results from scenario inputs.</summary>
internal static class PlanYearCalculator
{
    private const decimal InterestRate = 0.0017m;
    private const decimal DivQRate = 0.0094m;
    private const decimal DivUQRate = 0.0005m;
    private const decimal BrokerageEarnedRate = InterestRate + DivQRate + DivUQRate;


    /// <summary>Calculates appreciation, earnings, transfers, and expense funding in order.</summary>
    internal static void Calculate(PlanYear year)
    {
        AppreciateAccounts(year);
        CalculateEarnings(year);
        ApplyRmdsAndScheduledTransfers(year);
        ApplyExpenses(year);
    }

    internal static PlanYear FromScenario(
        Scenario scenario,
        decimal uncategorizedExpenseAmount)
    {
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
            Expenses = ForecastExpenses(scenario, uncategorizedExpenseAmount, scenario.CurrentAge)
        };
        BuildTransfers(year, scenario);
        return year;
    }

    internal static PlanYear ForecastFrom(PlanYear previousYear, Scenario scenario,
        decimal uncategorizedExpenseAmount)
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
            Expenses = ForecastExpenses(scenario, uncategorizedExpenseAmount, previousYear.EndingAge)
        };
        BuildTransfers(year, scenario);
        return year;
    }

    private static List<Expense> ForecastExpenses(Scenario scenario, decimal uncategorizedAmount, int age)
    {
        var uncategorized = new Expense
        {
            Name = "Uncategorized Expenses",
            Amount = uncategorizedAmount,
            AgeStart = scenario.CurrentAge,
            AgeEnd = scenario.LifeExpectancy,
            AnnualRateOfIncrease = scenario.AnnualInflationRate / 100m,
            UseInflationValue = true
        };
        return scenario.Expenses.Append(uncategorized)
            .Select(expense => ForecastExpense(expense, scenario.CurrentAge, age)).ToList();
    }

    /// <summary>Forecasts a copy from the original expense, rounding only at the target age.</summary>
    internal static Expense ForecastExpense(Expense source, int planStartAge, int age)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (age < planStartAge)
            throw new ArgumentOutOfRangeException(nameof(age), "Age cannot precede the plan-start age.");
        return new Expense(source)
        {
            Amount = source.IsActiveAtAge(age)
                ? ForecastAmount(source.Amount, source.AnnualRateOfIncrease, planStartAge, age)
                : 0m
        };
    }

    /// <summary>Forecasts the original transfer amount within its inclusive age boundaries.</summary>
    internal static decimal ForecastTransferAmount(ScheduledTransfer source, int planStartAge, int age,
        decimal inflationRate)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (age < source.AgeStart || age > source.AgeEnd) return 0m;
        var rate = source.UseInflationValue ? inflationRate : source.AnnualRateOfIncrease;
        return ForecastAmount(source.Amount, rate, planStartAge, age);
    }

    /// <summary>Income grows from plan start, including years before it begins paying.</summary>
    internal static decimal ForecastIncomeAmount(Income source, int planStartAge, int age)
        => age < source.AgeStart ? 0m
            : Math.Max(0m, ForecastAmount(source.InitialAmount, source.AnnualRateOfIncrease, planStartAge, age));

    private static decimal ForecastAmount(decimal amount, decimal rate, int planStartAge, int age)
    {
        var factor = 1m;
        for (var current = planStartAge; current < age; current++) factor *= 1m + rate;
        return Round(amount * factor);
    }

    /// <summary>Creates the RMD request from the account's beginning balance and starting age.</summary>
    internal static AccountTransfer CreateRmdTransfer(Account account, int startingAge)
        => new("RMD", account, null,
            RmdCalculator.Calculate(account.Type, account.BeginningBalance, startingAge), TransferReason.Rmd);

    private static void BuildTransfers(PlanYear year, Scenario scenario)
    {
        year.Transfers = year.Accounts.Select(account => CreateRmdTransfer(account, year.StartingAge))
            .Where(transfer => transfer.RequestedAmount > 0m).ToList();
        foreach (var income in scenario.Income)
        {
            var amount = ForecastIncomeAmount(income, scenario.CurrentAge, year.StartingAge);
            if (amount > 0m)
                year.Transfers.Add(new AccountTransfer(income.Name, null, null, amount, TransferReason.ExternalIncome));
        }
        foreach (var instruction in scenario.Transfers)
        {
            var amount = ForecastTransferAmount(instruction, scenario.CurrentAge, year.StartingAge, scenario.AnnualInflationRate / 100m);
            if (amount <= 0m) continue;
            var from = FindTransferAccount(year, instruction.FromAccountName);
            var to = FindTransferAccount(year, instruction.ToAccountName);
            year.Transfers.Add(new AccountTransfer(instruction.Name, from, to, amount, TransferReason.Scheduled));
        }
    }

    private static Account FindTransferAccount(PlanYear year, string name)
    {
        var matches = year.Accounts.Where(account => account.Name == name).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"Transfer account '{name}' must identify exactly one account.");
        return matches[0];
    }

    /// <summary>Applies annual appreciation to each account's beginning balance.</summary>
    internal static void AppreciateAccounts(PlanYear year)
    {
        foreach (var account in year.Accounts)
        {
            account.EndingBalance = decimal.Round(
                account.BeginningBalance * (1 + account.AppreciationRate),
                0,
                MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Calculates investment earnings from each account's beginning balance.</summary>
    internal static void CalculateEarnings(PlanYear year)
    {
        year.AccountEarnings = year.Accounts.Select(CalculateAccountEarnings).ToList();
    }

    /// <summary>Calculates the separately rounded earnings components for one account.</summary>
    internal static AccountEarnings CalculateAccountEarnings(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var balance = Math.Max(0m, account.BeginningBalance);
        if (HasBrokerageEquityIncome(account))
        {
            return new AccountEarnings(account, Round(balance * InterestRate),
                Round(balance * DivQRate), Round(balance * DivUQRate));
        }
        if (account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Bonds)
        {
            return new AccountEarnings(account,
                Round(balance * Math.Max(0m, account.AppreciationRate)), 0m, 0m);
        }
        return new AccountEarnings(account, 0m, 0m, 0m);
    }

    private static decimal Round(decimal amount)
        => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    internal static bool HasBrokerageEquityIncome(Account account)
        => account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Equities;

    internal static decimal GetEarnedRate(Account account)
    {
        if (HasBrokerageEquityIncome(account))
        {
            return BrokerageEarnedRate;
        }
        if (account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Bonds)
        {
            return Math.Max(0m, account.AppreciationRate);
        }
        return 0m;
    }

    internal static decimal GetTotalReturn(Account account)
        => account.AppreciationRate + GetEarnedRate(account);

    /// <summary>Executes RMDs first, followed by scheduled account-to-account transfers.</summary>
    internal static void ApplyRmdsAndScheduledTransfers(PlanYear year)
    {
        var earned = year.AccountEarnings.Sum(income => income.Amount);
        var required = year.Transfers.Where(transfer => transfer.Reason == TransferReason.Rmd)
            .Sum(transfer => Math.Min(transfer.RequestedAmount,
                Math.Max(0m, transfer.FromAccount!.EndingBalance)));

        // Check for a surplus destination before withdrawing any funds.
        FindSurplusDestination(year, year.ExternalIncome + earned + required);

        // RMDs move existing funds into the cash pool used for expenses.
        foreach (var transfer in year.Transfers.Where(transfer => transfer.Reason == TransferReason.Rmd))
            ApplyTransfer(transfer);

        // Explicit account-to-account instructions are separate from expense
        // funding. Required distributions take precedence over these movements.
        foreach (var transfer in year.Transfers.Where(transfer => transfer.Reason == TransferReason.Scheduled))
        {
            ApplyTransfer(transfer);
            year.UnfundedTransfers += transfer.RequestedAmount - transfer.Amount;
        }
    }

    /// <summary>Applies one movement, limiting its amount to the source's available balance.</summary>
    internal static decimal ApplyTransfer(AccountTransfer transfer)
    {
        transfer.Amount = transfer.FromAccount is null ? transfer.RequestedAmount
            : Math.Min(transfer.RequestedAmount, Math.Max(0m, transfer.FromAccount.EndingBalance));
        if (transfer.FromAccount is not null) transfer.FromAccount.EndingBalance -= transfer.Amount;
        if (transfer.ToAccount is not null) transfer.ToAccount.EndingBalance += transfer.Amount;
        return transfer.Amount;
    }

    private static Account? FindSurplusDestination(PlanYear year, decimal cash)
    {
        return cash > year.TotalExpenses
            ? year.Accounts.Where(account => account.Type == AccountType.Brokerage)
                .OrderBy(account => account.WithdrawalPriority).FirstOrDefault()
                ?? throw new InvalidOperationException("A Brokerage account is required to receive excess mandatory income.")
            : null;
    }

    /// <summary>Funds expenses from external income, earnings, and RMDs, reinvesting surplus or withdrawing any shortfall.</summary>
    internal static void ApplyExpenses(PlanYear year)
    {
        var cash = year.MandatoryCashFlow;
        var surplus = Math.Max(0m, cash - year.TotalExpenses);
        var destination = FindSurplusDestination(year, cash);

        if (destination is not null)
        {
            var reinvestment = new AccountTransfer("Unspent cash", null, destination,
                surplus, TransferReason.SurplusReinvestment);
            ApplyTransfer(reinvestment);
            year.Transfers.Add(reinvestment);
        }

        var remainingExpenses = Math.Max(0m, year.TotalExpenses - cash);
        foreach (var account in year.Accounts.OrderBy(account => account.WithdrawalPriority))
        {
            if (remainingExpenses <= 0m) break;
            var amount = Math.Min(Math.Max(0m, account.EndingBalance), remainingExpenses);
            if (amount <= 0m) continue;
            var withdrawal = new AccountTransfer("Expense funding", account, null,
                amount, TransferReason.ExpenseWithdrawal);
            remainingExpenses -= ApplyTransfer(withdrawal);
            year.Transfers.Add(withdrawal);
        }
        year.UnfundedExpenses = remainingExpenses;
    }
}
