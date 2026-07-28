namespace FinPlanner.Engine;

/// <summary>
/// Represents the mutable financial planState used while calculating a Plan.
///
/// The PlanState contains only information that persists from one
/// calendar year to the next. It is updated as each PlanYear is calculated
/// and serves as the starting point for the following year.
///
/// The original Scenario is never modified during plan calculation.
/// </summary>
internal sealed class PlanState
{
    /// <summary>
    /// Creates a PlanState from the starting values contained in a
    /// Scenario.
    /// </summary>
    /// <param name="scenario">
    /// The scenario used to initialize the planning planState.
    /// </param>
    public static PlanState Initialize(
        Scenario scenario,
        decimal annualExpenses)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var planState = new PlanState();

        // Add the accounts in the scenario to the planState
        foreach (var account in scenario.Accounts)
        {
            planState.Accounts.Add(new AccountState
            {
                AccountId = account.Id,
                Balance = account.Balance
            });
        }

        // Add the expenses in the scenario to the planState
        
        // First, all the named expenses
        foreach (var expense in scenario.Expenses)
        {
            planState.Expenses.Add(new Expense
            {
                Name = expense.Name,
                Amount = expense.Amount,
                AgeStart = expense.AgeStart,
                AgeEnd = expense.AgeEnd,
                AnnualRateOfIncrease = expense.AnnualRateOfIncrease
            });
        }
        // Then "all other expenses" as a single unnamed expense
        // Calculate "all other expenses" by subtracting the sum of all named expenses
        var namedExpenses = planState.Expenses.Sum(expense => expense.Amount);

        planState.Expenses.Add(new Expense
        {
            Name = "All other expenses",
            Amount = Math.Max(0m, annualExpenses - namedExpenses),
            AgeStart = scenario.CurrentAge,
            AgeEnd = scenario.LifeExpectancy,
            AnnualRateOfIncrease = scenario.AnnualInflationRate
        });

        return planState;
    }

    /// <summary>
    /// The current planState of every account participating in the plan.
    /// These balances are updated as each calendar year is calculated.
    /// </summary>
    public List<AccountState> Accounts { get; } = [];

    /// <summary>
    /// The current expenses that are active in the plan. These expenses are updated as each calendar year is calculated.
    /// </summary>
    public List<Expense> Expenses { get; } = [];

    /// <summary>
    /// Capital loss carryforward available for future tax years.
    /// </summary>
    public decimal CapitalLossCarryforward { get; set; }

    /// <summary>
    /// Charitable contribution carryforward available for future tax years.
    /// </summary>
    public decimal CharitableContributionCarryforward { get; set; }

    /// <summary>
    /// Any additional planState that must persist from one year to the next
    /// should be stored here.
    ///
    /// Examples include:
    /// - Roth conversion history
    /// - Prior-year MAGI
    /// - Net operating loss carryforwards
    /// - AMT credit carryforwards
    /// - Remaining lifetime exclusions
    /// </summary>
}
