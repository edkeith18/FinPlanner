namespace FinPlanner.Engine;

/// <summary>
/// Finds the largest annual-expense target that leaves a nonnegative final balance.
/// </summary>
public sealed class MaximumExpenseCalculator
{
    private const decimal ExpenseStep = 1_000m;
    private readonly PlanBuilder planBuilder;

    public MaximumExpenseCalculator(PlanBuilder planBuilder)
    {
        this.planBuilder = planBuilder;
    }

    public bool TryCalculate(
        Scenario scenario,
        out decimal maximumAnnualExpenses)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        maximumAnnualExpenses = 0m;

        if (scenario.Accounts.Count == 0
            || scenario.CurrentAge > scenario.LifeExpectancy)
        {
            return false;
        }

        var lowerBound = 0;
        var upperBound = Math.Max(
            1,
            (int)(scenario.Accounts.Sum(account => account.Balance)
                / ExpenseStep));
        var potentialMaximum = 0;

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + (upperBound - lowerBound) / 2;
            var annualExpenses = midpoint * ExpenseStep;
            var finalBalance = CalculateFinalBalance(
                scenario,
                annualExpenses);

            // A plan ending at exactly zero is considered unsuccessful.
            if (finalBalance > 0m)
            {
                potentialMaximum = midpoint;
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        maximumAnnualExpenses = potentialMaximum * ExpenseStep;

        // Preserve the existing rule that a plan ending at exactly zero fails.
        return CalculateFinalBalance(
            scenario,
            maximumAnnualExpenses) > 0m;
    }

    private decimal CalculateFinalBalance(
        Scenario scenario,
        decimal annualExpenses)
    {
        var plan = planBuilder.Build(
            scenario,
            new PlanBuildOptions
            {
                AnnualExpenses = annualExpenses
            });

        return plan.LastYear?.EndingBalance ?? 0m;
    }
}
