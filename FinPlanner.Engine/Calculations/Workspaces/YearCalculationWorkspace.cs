namespace FinPlanner.Engine;

/// <summary>
/// Represents the mutable working data used while calculating a single
/// PlanYear.
///
/// The YearCalculationWorkspace exists only for the duration of a single
/// year's calculation. It contains the evolving financial state, the
/// calculated results being accumulated, and any temporary values needed
/// by the calculation pipeline.
///
/// When calculation is complete, Complete() converts the working data into
/// an immutable PlanYear and advances the supplied PlanState so it is
/// ready for the following calendar year.
/// </summary>
internal sealed class YearCalculationWorkspace
{
    /// <summary>
    /// Creates a new calculation context for a single calendar year.
    /// </summary>
    /// <param name="scenario">
    /// The scenario for which to calculate.
    /// </param>
    /// <param name="planState">
    /// The mutable planning state at the beginning of the year.
    /// This object is updated during the calculation.
    /// </param>
    /// <param name="calendarYear">
    /// The calendar year being calculated.
    /// </param>
    public YearCalculationWorkspace(
        Scenario scenario,
        PlanState planState,
        int calendarYear)
    {
        Scenario = scenario;
        PlanState = planState;
        CalendarYear = calendarYear;
    }

    /// <summary>
    /// The calendar year currently being calculated.
    /// </summary>
    public int CalendarYear { get; }

    /// <summary>
    /// The original, unmodified inputs and assumptions for the plan.
    /// </summary>
    public Scenario Scenario { get; }

    /// <summary>
    /// The mutable financial state shared between all years of the plan.
    /// When this year's calculation is complete, this state will represent
    /// the beginning of the next calendar year.
    /// </summary>
    public PlanState PlanState { get; }

    /// <summary>
    /// The calculated account results for this year.
    /// One result exists for each account in the scenario.
    /// </summary>
    public List<AccountCalculationWorkspace> Accounts { get; } = [];

    public List<ExpenseYearResult> Expenses { get; } = [];

    /// <summary>
    /// The calculated taxable income for this year.
    /// </summary>
    public TaxableIncomeBreakdown Income { get; set; } = new();

    /// <summary>
    /// The calculated deductions for this year.
    /// </summary>
    public DeductionBreakdown Deductions { get; set; } = new();

    /// <summary>
    /// The calculated tax results for this year.
    /// </summary>
    public TaxYearResult Taxes { get; set; } = new();

    /// <summary>
    /// Creates the immutable PlanYear representing the completed year.
    ///
    /// This method also updates the PlanState so it contains the
    /// beginning balances and expense values required to calculate
    /// the following calendar year.
    /// </summary>
    public PlanYear Complete()
    {
        var accountResults = Accounts
            .Select(account => account.ToResult())
            .ToList();

        foreach (var accountState in PlanState.Accounts)
        {
            var result = accountResults.Single(
                account => account.Id == accountState.AccountId);

            accountState.Balance = result.EndingBalance;
        }

        foreach (var expense in PlanState.Expenses)
        {
            expense.Amount *= 1m + expense.AnnualRateOfIncrease / 100m;
        }

        return new PlanYear
        {
            CalendarYear = CalendarYear,
            Age = Scenario.CurrentAge + CalendarYear - Scenario.StartYear,
            Accounts = accountResults,
            Expenses = Expenses.ToList(),
            Taxes = Taxes
        };
    }
}
