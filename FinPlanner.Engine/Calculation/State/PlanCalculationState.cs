namespace FinPlanner.Engine;

/// <summary>
/// Represents the mutable financial state used while calculating a Plan.
///
/// The PlanCalculationState contains only information that persists from one
/// calendar year to the next. It is updated as each PlanYear is calculated
/// and serves as the starting point for the following year.
///
/// The original Scenario is never modified during plan calculation.
/// </summary>
internal sealed class PlanCalculationState
{
    /// <summary>
    /// Creates a PlanCalculationState from the starting values contained in a
    /// Scenario.
    /// </summary>
    /// <param name="scenario">
    /// The scenario used to initialize the planning state.
    /// </param>
    public static PlanCalculationState FromScenario(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var state = new PlanCalculationState();

        foreach (var account in scenario.Accounts)
        {
            state.Accounts.Add(new AccountCalculationState
            {
                AccountId = account.Id,
                Balance = account.Balance
            });
        }

        return state;
    }

    /// <summary>
    /// The current state of every account participating in the plan.
    /// These balances are updated as each calendar year is calculated.
    /// </summary>
    public List<AccountCalculationState> Accounts { get; } = [];

    /// <summary>
    /// Capital loss carryforward available for future tax years.
    /// </summary>
    public decimal CapitalLossCarryforward { get; set; }

    /// <summary>
    /// Charitable contribution carryforward available for future tax years.
    /// </summary>
    public decimal CharitableContributionCarryforward { get; set; }

    /// <summary>
    /// Any additional state that must persist from one year to the next
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
