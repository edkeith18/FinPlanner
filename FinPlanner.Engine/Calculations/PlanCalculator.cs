namespace FinPlanner.Engine;

public class PlanCalculator
{
    private const decimal ExpenseStep = 1_000m;

    /// <summary>
    /// Calculates the plan using the supplied annual expenses and other assumptions.
    /// </summary>
    public Plan CalculateCurrentTrajectory(Scenario scenario)
        => Build(new Scenario(scenario), "Financial Plan");

    /// <summary>
    /// Finds the highest sustainable initial annual expenses in $1,000 increments,
    /// keeping named expenses fixed and varying uncategorized expenses.
    /// </summary>
    public Plan CalculateMaximumSpend(Scenario scenario)
    {
        scenario = new Scenario(scenario);
        const string Name = "Maximum Spend Plan";
        if (scenario.EndYear <= scenario.StartYear)
        {
            return Build(scenario, Name);
        }

        Plan? bestPlan = null;
        bool CanSustain(decimal expenseSteps)
        {
            scenario.FirstYearAnnualExpenses = expenseSteps * ExpenseStep;
            var trialPlan = Build(scenario, Name);
            var isSustainable = trialPlan.IsSuccessful
                && (decimal.Truncate(trialPlan.PlanYears[^1].EndingBalance) > 0m
                    || trialPlan.PlanYears[^1].ExternalIncome > 0m);
            if (isSustainable)
            {
                bestPlan = trialPlan;
            }
            return isSustainable;
        }

        // Candidates below named expenses would require negative uncategorized expenses.
        var lowerBound = Math.Max(0m, decimal.Ceiling(
            (StartingNamedExpenses(scenario) + scenario.FirstYearTaxExpense) / ExpenseStep));
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

        return bestPlan ?? NoSustainablePlan(scenario, Name);
    }

    /// <summary>
    /// Finds the highest initial annual expenses in $1,000 increments whose final
    /// forecast year increases total account balances by at least the scenario inflation rate.
    /// </summary>
    public Plan CalculatePerpetualSpend(Scenario scenario)
    {
        scenario = new Scenario(scenario);
        const string Name = "Perpetual Spend Plan";
        return CalculatePerpetualSpend_Simple(scenario, Name);
    }

    private Plan CalculatePerpetualSpend_Simple(Scenario scenario, string name)
    {
        if (scenario.EndYear <= scenario.StartYear) return Build(scenario, name);

        Plan? bestPlan = null;
        bool MeetsGrowthTarget(decimal steps)
        {
            scenario.FirstYearAnnualExpenses = steps * ExpenseStep;
            Plan trial;
            try
            {
                trial = Build(scenario, name);
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
            (StartingNamedExpenses(scenario) + scenario.FirstYearTaxExpense) / ExpenseStep));
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
        var note = $"Spending targets at least {scenario.AnnualInflationRate}% total account balance growth in the final forecast year, in $1,000 budget increments. This forecast criterion does not prove indefinite sustainability.";
        scenario.FirstYearAnnualExpenses = bestPlan.AnnualExpenses;
        return new Plan(scenario, name, bestPlan.PlanYears.ToList(), true, calculationNote: note);
    }

    private static Plan NoSustainablePlan(Scenario scenario, string name)
    {
        scenario.FirstYearAnnualExpenses = 0m;
        return new Plan(scenario, name, [], false, "No sustainable spending plan was found.");
    }

    private static Plan Build(Scenario scenario, string name)
    {
        var years = new List<PlanYear>();
        if (scenario.EndYear <= scenario.StartYear)
        {
            return new Plan(scenario, name, years, false,
                "There are no remaining 12-month periods before life expectancy.");
        }

        var uncategorizedExpenses = scenario.FirstYearAnnualExpenses
            - scenario.FirstYearTaxExpense - StartingNamedExpenses(scenario);
        if (uncategorizedExpenses < 0m)
        {
            return new Plan(scenario, name, years, false, "First-year tax and named expenses exceed FirstYearAnnualExpenses.");
        }

        var currentYear = PlanYearCalculator.FromScenario(scenario, uncategorizedExpenses);
        while (true)
        {
            years.Add(currentYear);
            PlanYearCalculator.Calculate(currentYear, scenario.TaxInfo);
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
