namespace FinPlanner.Engine;

/// <summary>
/// Creates a financial Plan from a Scenario.
///
/// A Plan is calculated one year at a time, in chronological order.
/// Each year's calculations update the PlanCalculationState so that the ending
/// state of one year becomes the beginning state of the following year.
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
    public Plan Build(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var planYears = new List<PlanYear>();

        // PlanCalculationState contains the mutable financial state used while
        // calculating the plan. The original Scenario is not modified.
        var state = PlanCalculationState.FromScenario(scenario);

        // Years must be calculated in chronological order because each year's
        // ending balances and carryforward values are inputs to the next year.
        for (var calendarYear = scenario.StartYear;
             calendarYear <= scenario.EndYear;
             calendarYear++)
        {
            var planYear = CalculateYear(
                scenario,
                state,
                calendarYear);

            planYears.Add(planYear);

            // CalculateYear updates state in place. At this point, state
            // represents the beginning financial state of the next year.
        }

        return new Plan(
            DateTimeOffset.UtcNow,
            planYears);
    }

    /// <summary>
    /// Calculates the financial results for one calendar year.
    ///
    /// This method updates <paramref name="state"/> in place so that, when
    /// the method returns, the state contains the ending account balances
    /// and carryforward values needed to calculate the following year.
    /// </summary>
    /// <param name="scenario">
    /// The original scenario and its planning assumptions.
    /// </param>
    /// <param name="state">
    /// The mutable financial state at the beginning of the year.
    /// This state is advanced to the end of the year by this method.
    /// </param>
    /// <param name="calendarYear">
    /// The calendar year being calculated.
    /// </param>
    /// <returns>
    /// A completed PlanYear describing the financial activity and results
    /// for the specified calendar year.
    /// </returns>
    private static PlanYear CalculateYear(
        Scenario scenario,
        PlanCalculationState state,
        int calendarYear)
    {
        // The calculation context contains the working data and detailed
        // results accumulated while calculating this year.
        var context = new YearCalculationContext(
            scenario,
            state,
            calendarYear);

        /*
         * The order of these operations is part of the financial model.
         * Keeping the yearly calculation pipeline explicit makes it easier
         * to understand, test, and adjust.
         *
         * Initial proposed order:
         *
         * 1. Establish beginning account balances.
         * 2. Generate income.
         * 3. Generate expenses.
         * 4. Execute transfers and retirement distributions.
         * 5. Calculate taxable income and deductions.
         * 6. Calculate taxes.
         * 7. Pay expenses and taxes.
         * 8. Apply investment income and capital appreciation.
         * 9. Produce ending account balances.
         * 10. Advance PlanCalculationState for the following year.
         */

        InitializeAccounts(context);
        ApplyIncome(context);
        ApplyExpenses(context);
        ApplyTransfers(context);
        CalculateTaxes(context);
        PayExpensesAndTaxes(context);
        ApplyInvestmentReturns(context);

        // Complete creates the immutable PlanYear result and updates the
        // shared PlanCalculationState to represent the end of this calendar year.
        return context.Complete();
    }

    /// <summary>
    /// Initializes the working account results using the account balances
    /// contained in the beginning PlanCalculationState.
    /// </summary>
    private static void InitializeAccounts(
        YearCalculationContext context)
    {
        // TODO: Create a working yearly result for each account and copy
        // its beginning balance from PlanCalculationState.
    }

    /// <summary>
    /// Calculates all income received during the current calendar year
    /// and deposits the proceeds into the appropriate accounts.
    /// </summary>
    private static void ApplyIncome(
        YearCalculationContext context)
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
        YearCalculationContext context)
    {
        // TODO: Determine which expenses apply during this year and calculate
        // their inflation-adjusted or otherwise modified amounts.
    }

    /// <summary>
    /// Executes scheduled transfers, retirement distributions, Roth
    /// conversions, and other movements of money between accounts.
    /// </summary>
    private static void ApplyTransfers(
        YearCalculationContext context)
    {
        // TODO: Apply transfers while preserving the source account,
        // destination account, amount, and tax consequences.
    }

    /// <summary>
    /// Calculates taxable income, deductions, credits, and the resulting
    /// federal and state tax liability for the current year.
    /// </summary>
    private static void CalculateTaxes(
        YearCalculationContext context)
    {
        // TODO: Populate TaxableIncomeBreakdown, DeductionBreakdown,
        // TaxYearResult, NIIT, AMT, and other modeled tax values.
    }

    /// <summary>
    /// Removes the money needed to pay expenses and taxes from the
    /// appropriate accounts.
    /// </summary>
    private static void PayExpensesAndTaxes(
        YearCalculationContext context)
    {
        // TODO: Determine which accounts fund expenses and taxes, then record
        // the resulting account withdrawals or payments.
    }

    /// <summary>
    /// Applies qualified dividends, nonqualified dividends, interest,
    /// and capital appreciation to the applicable accounts.
    /// </summary>
    private static void ApplyInvestmentReturns(
        YearCalculationContext context)
    {
        // TODO: Calculate each type of investment return separately so its
        // effect on account balances and taxable income remains visible.
    }
}
