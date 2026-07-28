namespace FinPlanner.Engine;

/// <summary>
/// The calculated amount of an expense for one plan year.
/// </summary>
public sealed record ExpenseYearResult(
    string Name,
    decimal Amount);
