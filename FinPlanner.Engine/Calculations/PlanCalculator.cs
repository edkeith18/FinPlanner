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
        candidateScenario.FirstYearTaxExpense = 0m;

        // Set calculateFirstYearTaxes to true so that the first-year taxes are recalculated for each candidate budget
        // instead of using the original candidateScenario's FirstYearTaxExpense. This ensures that the plan is built with the correct tax calculations for the given budget.
        return Build(candidateScenario, name, calculateFirstYearTaxes: true);
    }

    /// <summary>
    /// Builds a plan from the scenario
    /// </summary>
    /// <param name="scenario"></param>
    /// <param name="name"></param>
    /// <param name="calculateFirstYearTaxes"></param>
    /// <returns></returns>
    private static Plan Build(Scenario scenario, string name, bool calculateFirstYearTaxes = false)
    {
        var years = new List<PlanYear>();
        var uncategorizedExpenses = scenario.FirstYearAnnualExpenses
            - scenario.FirstYearTaxExpense - StartingNamedExpenses(scenario);
        if (uncategorizedExpenses < 0m)
        {
            return new Plan(scenario, name, years, false, "First-year tax and named expenses exceed FirstYearAnnualExpenses.");
        }

        var currentYear = PlanYearCalculator.FromScenario(scenario, uncategorizedExpenses);

        // If calculateFirstYearTaxes is true, the first-year taxes will be recalculated based on the candidate budget.
        // This is necessary for the maximum-spend and perpetual-spend calculations, where the first-year budget is being adjusted to find the highest sustainable spending plan.
        if (calculateFirstYearTaxes)
        {
            // Named expenses are rounded individually when creating the year.
            // Put the rounding remainder in discretionary spending so the funded
            // candidate budget stays exact.
            var uncategorizedExpense = currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized);
            uncategorizedExpense.Amount = scenario.FirstYearAnnualExpenses
                - currentYear.Expenses.Where(expense => expense != uncategorizedExpense).Sum(expense => expense.Amount);
        }

        while (true)
        {
            years.Add(currentYear);
            PlanYearCalculator.Calculate(currentYear, scenario.TaxInfo);
            if (currentYear.YearsInFuture == 0 && calculateFirstYearTaxes)
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
            if (currentYear.YearsInFuture == 0)
                uncategorizedExpenses = currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount;
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

            currentYear = PlanYearCalculator.ForecastFrom(currentYear, scenario, uncategorizedExpenses);
        }
    }

    // FirstYearAnnualExpenses describes the starting-period budget. Future and expired
    // named expenses must not reduce its uncategorized portion.
    private static decimal StartingNamedExpenses(Scenario scenario)
        => scenario.Expenses.Where(expense => expense.IsActiveAtAge(scenario.CurrentAge))
            .Sum(expense => expense.Amount);

}
