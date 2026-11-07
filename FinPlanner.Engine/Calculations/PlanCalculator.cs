namespace FinPlanner.Engine;

public class PlanCalculator
{
    /// <summary>
    /// Calculates the plan using the data from the scenario
    /// </summary>
    public Plan CalculateCurrentTrajectory(Scenario scenario)
        => Build(new Scenario(scenario), "Financial Plan");

    /// <summary>
    /// Finds the highest sustainable initial annual expenses,
    /// keeping named expenses fixed and recalculating first-year taxes for each budget.
    /// </summary>
    public Plan CalculateMaximumSpend(Scenario scenario)
    {
        // The amount of annual expenses to test is incremented in $1,000 steps.
        var ExpenseStep = 1_000m;

        scenario = new Scenario(scenario);
        const string PlanName = "Maximum Spend Plan";

        Plan? bestPlan = null;
        bool CanSustain(decimal expenseSteps)
        {
            var trialPlan = BuildSearchCandidate(scenario, expenseSteps * ExpenseStep, PlanName);
            var isSustainable = trialPlan.IsSuccessful
                && (decimal.Truncate(trialPlan.PlanYears[^1].EndingBalance) > 0m
                    || trialPlan.PlanYears[^1].ExternalIncome > 0m);
            if (isSustainable)
            {
                bestPlan = trialPlan;
            }
            return isSustainable;
        }

        // Candidates must cover named expenses and the calculated taxes to fund them.
        var lowerBound = Math.Max(0m, decimal.Ceiling(
            PlanYearCalculator.CalculateMinimumFirstYearBudget(scenario) / ExpenseStep));
        var upperBound = Math.Max(lowerBound, Math.Max(1m, decimal.Ceiling(
            scenario.Accounts.Sum(account => account.BeginningBalance) / ExpenseStep)));

        while (CanSustain(upperBound))
        {
            lowerBound = upperBound + 1;
            upperBound *= 2;
        }

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + decimal.Floor((upperBound - lowerBound) / 2);
            if (CanSustain(midpoint))
            {
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        return bestPlan ?? NoSustainablePlan(scenario, PlanName);
    }

    /// <summary>
    /// Finds the highest initial annual expenses in $1,000 increments whose final
    /// forecast year increases total account balances by at least the scenario inflation rate.
    /// First-year taxes are recalculated for each candidate budget.
    /// </summary>
    public Plan CalculatePerpetualSpend(Scenario scenario)
    {
        scenario = new Scenario(scenario);
        const string Name = "Perpetual Spend Plan";
        return CalculatePerpetualSpend_Simple(scenario, Name);
    }

    private Plan CalculatePerpetualSpend_Simple(Scenario scenario, string name)
    {
        // The amount of annual expenses to test is incremented in $1,000 steps.
        var ExpenseStep = 1_000m;

        Plan? bestPlan = null;
        bool MeetsGrowthTarget(decimal steps)
        {
            Plan trial;
            try
            {
                trial = BuildSearchCandidate(scenario, steps * ExpenseStep, name);
            }
            catch (OverflowException)
            {
                return false;
            }
            if (!trial.IsSuccessful) return false;
            var previousBalance = trial.PlanYears.Count > 1
                ? trial.PlanYears[^2].EndingBalance
                : scenario.Accounts.Sum(account => account.BeginningBalance);
            if (previousBalance <= 0m || trial.PlanYears[^1].EndingBalance < previousBalance * (1m + scenario.AnnualInflationRate / 100m))
                return false;
            bestPlan = trial;
            return true;
        }

        var lower = Math.Max(0m, decimal.Ceiling(
            PlanYearCalculator.CalculateMinimumFirstYearBudget(scenario) / ExpenseStep));
        var upper = Math.Max(lower, Math.Max(1m, decimal.Ceiling(
            scenario.Accounts.Sum(account => account.BeginningBalance) / ExpenseStep)));
        while (MeetsGrowthTarget(upper)) { lower = upper + 1; upper *= 2; }
        while (lower <= upper)
        {
            var middle = lower + decimal.Floor((upper - lower) / 2);
            if (MeetsGrowthTarget(middle)) lower = middle + 1;
            else upper = middle - 1;
        }

        if (bestPlan is null)
            return new Plan(scenario, name, [], false,
                $"No spending budget achieved at least {scenario.AnnualInflationRate}% total account balance growth in the final forecast year.");
        scenario.FirstYearAnnualExpenses = bestPlan.AnnualExpenses;
        return new Plan(scenario, name, bestPlan.PlanYears.ToList(), true);
    }

    private static Plan NoSustainablePlan(Scenario scenario, string name)
    {
        scenario.FirstYearAnnualExpenses = 0m;
        return new Plan(scenario, name, [], false, "No sustainable spending plan was found.");
    }

    private static Plan BuildSearchCandidate(Scenario scenario, decimal budget, string name)
    {
        scenario = new Scenario(scenario);
        scenario.FirstYearAnnualExpenses = budget;
        scenario.FirstYearTaxExpense = 0m;
        return Build(scenario, name, calculateFirstYearTaxes: true);
    }

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
        if (calculateFirstYearTaxes)
        {
            // Named expenses are rounded individually when creating the year.
            // Put the rounding remainder in discretionary spending so the funded
            // candidate budget stays exact.
            var discretionary = currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized);
            discretionary.Amount = scenario.FirstYearAnnualExpenses
                - currentYear.Expenses.Where(expense => expense != discretionary).Sum(expense => expense.Amount);
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
                var discretionary = currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized);
                discretionary.Amount -= calculatedTax;
                currentYear.Expenses.Single(expense => expense.Type == ExpenseType.Tax).Amount = calculatedTax;
                if (discretionary.Amount < 0m)
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
