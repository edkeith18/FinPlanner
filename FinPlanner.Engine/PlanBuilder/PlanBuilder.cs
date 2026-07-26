namespace FinPlanner.Engine;

/// <summary>
/// Creates a financial Plan from a Scenario.
///
/// A Plan is calculated one year at a time, in chronological order.
/// Each year's calculations update the PlanState so that the ending
/// planState of one year becomes the beginning planState of the following year.
/// </summary>
public sealed class PlanBuilder
{
    /// <summary>
    /// Calculates a complete financial plan for the supplied scenario.
    /// </summary>
    /// <param name="scenario">
    /// The scenario containing the accounts, income, expenses, transfers,
    /// tax assumptions, and planning period used to calculate the plan.
    /// </param>
    /// <returns>
    /// A completed Plan containing an ordered collection of PlanYear results.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scenario"/> is null.
    /// </exception>
    public Plan Build(
        Scenario scenario,
        PlanBuildOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var planYears = new List<PlanYear>();

        // This is the stop condition for the plan calculation loop. If the current age is already
        // greater than the life expectancy, no more plan years are calculated.
        if (scenario.CurrentAge > scenario.LifeExpectancy)
        {
            return new Plan(planYears);
        }

        var annualExpenses = options?.AnnualExpenses
            ?? scenario.AnnualExpenses;

        // PlanState contains the mutable financial planState used while
        // calculating the plan. The original Scenario is not modified.
        var planState = PlanState.Initialize(
            scenario,
            annualExpenses);
        string? failureReason = null;

        // PlanYears must be calculated in chronological order because each year's
        // ending balances and carryforward values are inputs to the next year.
        for (var calendarYear = scenario.StartYear;
             calendarYear <= scenario.EndYear;
             calendarYear++)
        {
            var planYear = Calculate(
                scenario,
                planState,
                calendarYear,
                out var unfundedWithdrawals);

            planYears.Add(planYear);

            // Calculate updates to planState in place. At this point, planState
            // represents the beginning financial planState of the next year.
            if (unfundedWithdrawals > 0m)
            {
                failureReason =
                    $"Account balances were insufficient to fund all required withdrawals in {calendarYear}.";
                break;
            }

            if (calendarYear < scenario.EndYear
                && planState.Accounts.All(account => account.Balance <= 0m))
            {
                failureReason =
                    $"All account balances were exhausted in {calendarYear} before the plan's final year.";
                break;
            }
        }

        return new Plan(
            planYears,
            failureReason is null,
            failureReason);
    }

    /// <summary>
    /// Calculates the financial results for one calendar year.
    ///
    /// This method updates <paramref name="planState"/> in place so that, when
    /// the method returns, the planState contains the ending account balances
    /// and carryforward values needed to calculate the following year.
    /// </summary>
    /// <param name="scenario">
    /// The original scenario and its planning assumptions.
    /// </param>
    /// <param name="planState">
    /// The mutable financial planState at the beginning of the year.
    /// This planState is advanced to the end of the year by this method.
    /// </param>
    /// <param name="calendarYear">
    /// The calendar year being calculated.
    /// </param>
    /// <returns>
    /// A completed PlanYear describing the financial activity and results
    /// for the specified calendar year.
    /// </returns>
    private static PlanYear Calculate(
        Scenario scenario,
        PlanState planState,
        int calendarYear,
        out decimal unfundedWithdrawals)
    {
        // The calculation workspace contains the working data and detailed
        // results accumulated while calculating this year.
        var workspace = new YearCalculationWorkspace(
            scenario,
            planState,
            calendarYear);

        /*
         * The order of these operations is part of the financial model.
         * Keeping the yearly calculation pipeline explicit makes it easier
         * to understand, test, and adjust.
         *
         * Order of events:
         *
         * 1. Establish beginning account balances.
         * 2. Apply investment income and capital appreciation.
         * 3. Apply income.
         * 4. Apply expenses.
         * 5. Execute transfers and retirement distributions.
         * 6. Calculate taxable income and deductions.
         * 7. Calculate taxes.
         * 8. Pay expenses and taxes.
         * 9. Produce ending account balances.
         * 10. Advance PlanState for the following year.
         */

        InitializeAccounts(workspace);
        ApplyInvestmentReturns(workspace);
        ApplyIncome(workspace);
        ApplyExpenses(workspace);
        ExecuteTransfers(workspace);
        CalculateTaxes(workspace);
        unfundedWithdrawals = PayExpensesAndTaxes(workspace);

        // Complete creates the immutable PlanYear result and updates the
        // shared PlanState to represent the end of this calendar year.
        return workspace.Complete();
    }

    /// <summary>
    /// Initializes the working account results using the account balances
    /// contained in the beginning PlanState.
    /// </summary>
    private static void InitializeAccounts(
        YearCalculationWorkspace workspace)
    {
        var accountsByWithdrawalPriority = workspace.Scenario.Accounts
            .OrderBy(account => account.WithdrawalPriority)
            .ToList();

        foreach (var account in accountsByWithdrawalPriority)
        {
            var accountState = workspace.PlanState.Accounts.Single(
                state => state.AccountId == account.Id);

            workspace.Accounts.Add(new AccountCalculationWorkspace
            {
                AccountId = accountState.AccountId,
                AccountName = account.Name,
                BeginningBalance = accountState.Balance,

                RateOfReturn = account.Holdings switch
                {
                    AccountHoldings.Equities =>
                        workspace.Scenario.SecuritiesAnnualRateOfReturn / 100m,

                    AccountHoldings.Bonds =>
                        workspace.Scenario.BondsAnnualRateOfReturn / 100m,

                    _ => throw new InvalidOperationException(
                        $"Unsupported holdings type: {account.Holdings}")
                }
            });
        }

        if (accountsByWithdrawalPriority.Count == 0)
        {
            var accountState = workspace.PlanState.Accounts.Single();
            workspace.Accounts.Add(new AccountCalculationWorkspace
            {
                AccountId = accountState.AccountId,
                AccountName = "Unfunded balance",
                BeginningBalance = accountState.Balance
            });
        }
    }

    /// <summary>
    /// Applies qualified dividends, nonqualified dividends, interest,
    /// and capital appreciation to the applicable accounts.
    /// </summary>
    private static void ApplyInvestmentReturns(
        YearCalculationWorkspace context)
    {
        foreach (var account in context.Accounts)
        {
            var rate =
                context.Scenario.SecuritiesAnnualRateOfReturn / 100m;

            account.CapitalAppreciation =
                account.BeginningBalance * account.RateOfReturn;
        }
    }

    /// <summary>
    /// Calculates all income received during the current calendar year
    /// and deposits the proceeds into the appropriate accounts.
    /// </summary>
    private static void ApplyIncome(
        YearCalculationWorkspace context)
    {
        // TODO: Determine which income sources apply during this year,
        // calculate their amounts, and record their destinations.
    }

    /// <summary>
    /// Calculates all expenses incurred during the current calendar year.
    ///
    /// This step identifies the expenses but does not necessarily remove
    /// money from accounts. Funding decisions may occur later in the yearly
    /// calculation pipeline.
    /// </summary>
    private static void ApplyExpenses(
        YearCalculationWorkspace context)
    {
        var age = context.Scenario.CurrentAge
            + context.CalendarYear
            - context.Scenario.StartYear;

        foreach (var expense in context.Scenario.Expenses)
        {
            context.Expenses.Add(new ExpenseYearResult(
                expense.Name,
                CalculateExpenseAmount(expense, age)));
        }

        if (context.CalendarYear > context.Scenario.StartYear)
        {
            var inflationRate =
                context.Scenario.AnnualInflationRate / 100m;

            context.PlanState.DiscretionaryExpenses *=
                1m + inflationRate;
        }

        context.DiscretionaryExpenses =
            context.PlanState.DiscretionaryExpenses;
    }

    /// <summary>
    /// Executes scheduled transfers, retirement distributions, Roth
    /// conversions, and other movements of money between accounts.
    /// </summary>
    private static void ExecuteTransfers(
        YearCalculationWorkspace context)
    {
        // TODO: Apply transfers while preserving the source account,
        // destination account, amount, and tax consequences.
    }

    /// <summary>
    /// Calculates taxable income, deductions, credits, and the resulting
    /// federal and planState tax liability for the current year.
    /// </summary>
    private static void CalculateTaxes(
        YearCalculationWorkspace context)
    {
        // TODO: Populate TaxableIncomeBreakdown, DeductionBreakdown,
        // TaxYearResult, NIIT, AMT, and other modeled tax values.
    }

    /// <summary>
    /// Removes the money needed to pay expenses and taxes from the
    /// appropriate accounts.
    /// </summary>
    private static decimal PayExpensesAndTaxes(
        YearCalculationWorkspace context)
    {
        var expenseWithdrawals = context.Expenses.Sum(
            expense => expense.Amount)
            + context.DiscretionaryExpenses;

        var unfundedExpenses = ApplyWithdrawals(
            context.Accounts,
            expenseWithdrawals,
            static (account, amount) =>
                account.ExpenseWithdrawals += amount);

        var unfundedTaxes = ApplyWithdrawals(
            context.Accounts,
            context.Taxes.TotalTax,
            static (account, amount) =>
                account.TaxWithdrawals += amount);

        return unfundedExpenses + unfundedTaxes;
    }

    private static decimal ApplyWithdrawals(
        IEnumerable<AccountCalculationWorkspace> accounts,
        decimal amount,
        Action<AccountCalculationWorkspace, decimal> apply)
    {
        var remaining = amount;

        foreach (var account in accounts)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var availableBalance = Math.Max(0m, account.EndingBalance);
            var withdrawal = Math.Min(remaining, availableBalance);

            apply(account, withdrawal);
            remaining -= withdrawal;
        }

        return remaining;
    }

    private static decimal CalculateExpenseAmount(
        Expense expense,
        int age)
    {
        if (age < expense.AgeStart || age > expense.AgeEnd)
        {
            return 0m;
        }

        var amount = expense.Amount;
        var rate = expense.AnnualRateOfIncrease / 100m;

        for (var year = expense.AgeStart; year < age; year++)
        {
            amount *= 1m + rate;
        }

        return amount;
    }
}
