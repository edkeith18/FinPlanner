namespace FinPlanner.Engine;

public class PlanCalculator
{
    private enum SpendingTarget
    {
        Maximum,
        Perpetual
    }

    /// <summary>
    /// Calculates the plan using the data from the candidateScenario
    /// </summary>
    public Plan CalculateCurrentTrajectory(Scenario scenario)
        => Build(new Scenario(scenario), "Financial Plan");

    /// <summary>
    /// Finds the highest initial annual expenses fully funded through life expectancy,
    /// keeping named expenses fixed and recalculating first-year taxes for each budget.
    /// </summary>
    public Plan CalculateMaximumSpend(Scenario scenario)
    {
        return FindHighestSpendingPlan(new Scenario(scenario), "Maximum Spend Plan", SpendingTarget.Maximum);
    }

    /// <summary>
    /// Finds the highest initial annual expenses whose final
    /// forecast year increases total account balances by at least the candidateScenario inflation rate.
    /// First-year taxes are recalculated for each candidate budget.
    /// With fewer than two forecast years, uses maximum-spend rules while retaining the perpetual plan name.
    /// </summary>
    public Plan CalculatePerpetualSpend(Scenario scenario)
    {

        // If the candidateScenario has fewer than two forecast years, a perpetual plan collapses down to a maximum-spend plan.
        var spendingTarget = scenario.LifeExpectancy <= scenario.CurrentAge + 1
            ? SpendingTarget.Maximum
            : SpendingTarget.Perpetual;

        return FindHighestSpendingPlan(new Scenario(scenario), "Perpetual Spend Plan", spendingTarget);
    }

    private static Plan FindHighestSpendingPlan(
        Scenario scenario, string planName, SpendingTarget target)
    {
        // The amount of annual expenses to test is incremented in $1,000 steps.
        const decimal ExpenseStep = 1_000m;

        Plan? bestPlan = null;

        bool MeetsTarget(decimal expenseStepCount)
        {
            var trial = BuildSearchCandidate(scenario, expenseStepCount * ExpenseStep, planName);

            // If the trial plan is not successful, it cannot meet the target.
            if (!trial.IsSuccessful) return false;

            // Check the target conditions for the trial plan.
            switch (target)
            {
                case SpendingTarget.Maximum:
                    // A fully funded forecast may finish with no remaining assets, so just return true since the plan is successful.
                    break;

                case SpendingTarget.Perpetual:
                    // The final year's balance growth must meet the candidateScenario inflation rate.
                    var previousBalance = trial.PlanYears[^2].EndingBalance;

                    if (previousBalance <= 0m
                        || trial.PlanYears[^1].EndingBalance < previousBalance * (1m + scenario.AnnualInflationRate / 100m))
                        return false;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }

            bestPlan = trial;
            return true;
        }

        // Candidates must cover named expenses and the calculated taxes to fund them.
        var lowerBound = Math.Max(0m, decimal.Ceiling(
            PlanYearCalculator.CalculateMinimumFirstYearBudget(scenario) / ExpenseStep));

        var upperBound = Math.Max(lowerBound, Math.Max(1m, decimal.Ceiling(
            scenario.Accounts.Sum(account => account.BeginningBalance) / ExpenseStep)));

        while (MeetsTarget(upperBound))
        {
            lowerBound = upperBound + 1;
            upperBound *= 2;
        }

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + decimal.Floor((upperBound - lowerBound) / 2);
            if (MeetsTarget(midpoint))
            {
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        // Return the best plan found, or a failure plan if no sustainable plan was found.
        return bestPlan ?? new Plan(scenario, planName, [], false, "No sustainable spending plan was found.");
    }

    private static Plan BuildSearchCandidate(Scenario scenario, decimal budget, string name)
    {
        // Create a copy of the scenario that was passed in, so that the original scenario is not modified by the search.
        var candidateScenario = new Scenario(scenario);

        candidateScenario.FirstYearAnnualExpenses = budget;
        candidateScenario.FirstYearTaxExpense = null;
        return Build(candidateScenario, name);
    }

    /// <summary>
    /// Builds a plan from the scenario
    /// </summary>
    /// <param name="scenario"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private static Plan Build(Scenario scenario, string name)
    {
        var years = new List<PlanYear>();

        // Figure out how much of the first-year expenses is uncategorized, after subtracting named expenses and the first-year tax expense.
        var uncategorizedAmount = scenario.FirstYearAnnualExpenses - StartingNamedExpenses(scenario)
            - scenario.FirstYearTaxExpense.GetValueOrDefault();

        // Verify that plan does not start with negative uncategorized expenses, which would indicate that the first-year budget is insufficient to cover named expenses and taxes.
        if (uncategorizedAmount < 0m)
        {
            return new Plan(scenario, name, years, false, "First-year tax and named expenses exceed FirstYearAnnualExpenses.");
        }

        // Create expenses List, adding uncategorized to named expenses
        var expenses = PlanYearCalculator.CreateExpenses(scenario, uncategorizedAmount);

        // Create the first plan year from the scenario and expenses. This will be the starting point for the plan calculation
        var currentYear = PlanYearCalculator.FromScenario(scenario, expenses);

        while (true)
        {
            years.Add(currentYear);
            PlanYearCalculator.Calculate(currentYear, scenario.TaxInfo);

            // FirstYearTaxExpense is null when the plan is being built as part of a spending search,
            // so the first-year taxes must be calculated and reclassified from uncategorized expenses.
            if (scenario.FirstYearTaxExpense is null)
            {
                // First year only
                if (currentYear.YearsInFuture == 0)
                {
                    // Withdrawals were funded at the candidate's fixed total budget.
                    // Reclassify part of that budget as tax, rather than adding tax
                    // on top or changing the withdrawals that produced it.
                    currentYear.Taxes = TaxCalculator.Calculate(currentYear, scenario.TaxInfo);
                    var calculatedTax = decimal.Round(currentYear.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero);
                    var uncategorizedExpense = currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized);
                    uncategorizedExpense.Amount -= calculatedTax;
                    currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Tax).Amount = calculatedTax;
                    if (uncategorizedExpense.Amount < 0m)
                        return new Plan(scenario, name, years, false, "First-year tax and named expenses exceed FirstYearAnnualExpenses.");
                }
            }

            // Preserve finalized first-year spending as the inflation baseline for future years.
            if (currentYear.YearsInFuture == 0)
            {
                expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount =
                    currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount;
            }

            if (!currentYear.IsFullyFunded)
            {
                return new Plan(scenario, name, years, false,
                    currentYear.UnfundedTransfers > 0m
                        ? $"Scheduled transfers cannot be fully funded at year {currentYear.StartingYear}."
                        : $"Plan fails at year {currentYear.StartingYear}.");
            }

            if (currentYear.EndingYear >= scenario.EndYear)
            {
                return new Plan(scenario, name, years, true);
            }

            currentYear = PlanYearCalculator.ForecastFrom(currentYear, scenario, expenses);
        }
    }

    // FirstYearAnnualExpenses describes the starting-period budget. Future and expired
    // named expenses must not reduce its uncategorized portion.
    private static decimal StartingNamedExpenses(Scenario scenario)
        => scenario.Expenses.Where(expense => expense.IsActiveAtAge(scenario.CurrentAge))
            .Sum(expense => expense.Amount);

}
