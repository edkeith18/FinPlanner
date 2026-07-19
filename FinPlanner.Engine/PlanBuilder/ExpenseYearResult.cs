namespace FinPlanner.Engine;

/// <summary>
/// The calculated amount of a named expense for one plan year.
/// </summary>
public sealed record ExpenseYearResult(
    string Name,
    decimal Amount);
