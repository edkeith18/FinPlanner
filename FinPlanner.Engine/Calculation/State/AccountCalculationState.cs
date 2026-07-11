namespace FinPlanner.Engine;
internal sealed class AccountCalculationState
{
    /// <summary>
    /// The Id of the account represented by this state.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The account balance at the current point in the simulation.
    /// </summary>
    public decimal Balance { get; set; }
}
