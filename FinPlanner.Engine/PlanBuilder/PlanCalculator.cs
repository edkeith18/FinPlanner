namespace FinPlanner.Engine;

public class PlanCalculator
{
    private const decimal ExpenseStep = 1_000m;
    private const int MaximumProofYears = 256;

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
            scenario.AnnualExpenses = expenseSteps * ExpenseStep;
            var trialPlan = Build(scenario, Name);
            var isSustainable = trialPlan.IsSuccessful
                && decimal.Truncate(trialPlan.PlanYears[^1].EndingBalance) > 0m;
            if (isSustainable)
            {
                bestPlan = trialPlan;
            }
            return isSustainable;
        }

        // Candidates below named expenses would require negative uncategorized expenses.
        var lowerBound = Math.Max(0m, decimal.Ceiling(
            scenario.Expenses.Sum(expense => expense.Amount) / ExpenseStep));
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
    /// Calculates a plan whose initial annual expenses can be sustained indefinitely.
    /// Lifetime named expenses continue at their own growth rates; uncategorized
    /// expenses grow with inflation forever. The returned years cover the scenario's
    /// original lifetime for reporting.
    /// </summary>
    /// <exception cref="NotSupportedException">An input balance, expense, or rate is negative.</exception>
    /// <exception cref="InvalidOperationException">The perpetual proof is inconclusive.</exception>
    public Plan CalculatePerpetualSpend(Scenario scenario)
    {
        scenario = new Scenario(scenario);
        const string Name = "Perpetual Spend Plan";
        var annualExpenses = FindPerpetualAnnualExpenses(new Scenario(scenario));
        if (annualExpenses is null)
        {
            return NoSustainablePlan(scenario, Name);
        }

        scenario.AnnualExpenses = annualExpenses.Value;
        return Build(scenario, Name);
    }

    private static Plan NoSustainablePlan(Scenario scenario, string name)
    {
        scenario.AnnualExpenses = 0m;
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

        var uncategorizedExpenses = scenario.AnnualExpenses
            - scenario.Expenses.Sum(expense => expense.Amount);
        if (uncategorizedExpenses < 0m)
        {
            return new Plan(scenario, name, years, false, "Known expenses exceed AnnualExpenses.");
        }

        var currentYear = PlanYear.FromScenario(scenario, uncategorizedExpenses);
        while (true)
        {
            years.Add(currentYear);
            if (CalculateYear(currentYear) > 0m)
            {
                return new Plan(scenario, name, years, false,
                    $"Plan fails at year {currentYear.StartingYear}.");
            }

            if (currentYear.EndingYear >= scenario.EndYear)
            {
                return new Plan(scenario, name, years, true);
            }

            currentYear = PlanYear.ForecastFrom(currentYear);
        }
    }

    // Shared by finite forecasts and the perpetual sustainability proof.
    private static decimal CalculateYear(PlanYear year)
    {
        foreach (var account in year.Accounts)
        {
            account.Appreciate();
        }
        return year.ApplyExpenses();
    }

    private static decimal? FindPerpetualAnnualExpenses(Scenario scenario)
    {
        if (scenario.AnnualInflationRate < 0m
            || scenario.SecuritiesAnnualRateOfReturn < 0m
            || scenario.BondsAnnualRateOfReturn < 0m
            || scenario.Accounts.Any(account => account.BeginningBalance < 0m)
            || scenario.Expenses.Any(expense => expense.Amount < 0m || expense.AnnualRateOfIncrease < 0m))
        {
            throw new NotSupportedException(
                "The perpetual calculation requires nonnegative balances, expenses, returns, and inflation.");
        }

        // Preserve the original lifetime boundary when identifying ongoing expenses.
        foreach (var expense in scenario.Expenses.Where(
            expense => expense.AgeEnd >= scenario.LifeExpectancy))
        {
            expense.AgeEnd = int.MaxValue;
        }

        // Life expectancy limits the report, not ongoing expense streams.
        scenario.LifeExpectancy = int.MaxValue;
        var namedExpenses = scenario.Expenses.Sum(expense => expense.Amount);
        var lowerBound = Math.Max(0m, decimal.Ceiling(namedExpenses / ExpenseStep));
        var upperBound = Math.Max(lowerBound, Math.Max(1m, decimal.Ceiling(
            scenario.Accounts.Sum(account => account.BeginningBalance) / ExpenseStep)));
        decimal? maximumExpenses = null;

        bool CanSustainCandidate(decimal steps)
        {
            scenario.AnnualExpenses = steps * ExpenseStep;
            return CanSustainPerpetually(scenario, scenario.AnnualExpenses - namedExpenses);
        }

        while (CanSustainCandidate(upperBound))
        {
            maximumExpenses = upperBound * ExpenseStep;
            lowerBound = upperBound + 1;
            upperBound *= 2;
        }

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + decimal.Floor((upperBound - lowerBound) / 2);
            if (CanSustainCandidate(midpoint))
            {
                maximumExpenses = midpoint * ExpenseStep;
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        return maximumExpenses;
    }

    private static bool CanSustainPerpetually(Scenario scenario, decimal uncategorizedExpenses)
    {
        var year = PlanYear.FromScenario(scenario, uncategorizedExpenses);
        // FromScenario always appends this expense after the named expenses.
        year.Expenses[^1].AgeEnd = int.MaxValue;
        if (year.Accounts.Sum(account => account.BeginningBalance) <= 0m)
        {
            return false;
        }

        var inflation = scenario.AnnualInflationRate / 100m;
        var streams = scenario.Expenses
            .Where(expense => expense.AgeEnd == int.MaxValue)
            .Select(expense => (Amount: expense.Amount, Rate: expense.AnnualRateOfIncrease))
            .Append((Amount: uncategorizedExpenses, Rate: inflation))
            .Where(stream => stream.Rate > 0m
                ? stream.Amount > 0m
                : decimal.Round(stream.Amount, 0, MidpointRounding.AwayFromZero) > 0m)
            .ToArray();
        var maximumReturn = year.Accounts.Max(account => account.AppreciationRate);
        var maximumExpenseGrowth = streams.Select(stream => stream.Rate).DefaultIfEmpty(0m).Max();
        var hasSurplusDestination = year.Accounts.Any(account => account.Type == AccountType.Brokerage);

        // Even the fastest-growing account cannot fund a perpetual stream that
        // grows at least as fast as its return. Finite named expenses cannot help.
        if (streams.Any(stream => stream.Rate >= maximumReturn))
        {
            return false;
        }

        // Proofs apply after finite expenses end and every ongoing expense starts.
        var lastTransitionAge = scenario.Expenses
            .Where(expense => expense.Amount > 0m)
            .Select(expense => expense.AgeEnd == int.MaxValue ? expense.AgeStart - 1 : expense.AgeEnd)
            .DefaultIfEmpty(scenario.CurrentAge - 1)
            .Max();

        try
        {
            for (var index = 0; index < MaximumProofYears; index++)
            {
                if (CalculateYear(year) > 0m || year.EndingBalance <= 0m)
                {
                    return false;
                }

                for (var streamIndex = 0; streamIndex < streams.Length; streamIndex++)
                {
                    streams[streamIndex].Amount *= 1m + streams[streamIndex].Rate;
                }
                // Do not prove future transfers feasible without a destination.
                if (year.StartingAge > lastTransitionAge
                    && (hasSurplusDestination || year.Accounts.All(account =>
                        account.Type != AccountType.Inherited || account.EndingBalance == 0m)))
                {
                    if (streams.Length == 0)
                    {
                        return true;
                    }

                    // With constant future spending, a componentwise nondecreasing
                    // account state stays nondecreasing under the same annual rules.
                    if (maximumExpenseGrowth == 0m && year.Accounts.All(
                        account => account.EndingBalance >= account.BeginningBalance))
                    {
                        return true;
                    }

                    // A currently empty Brokerage account may receive future surplus,
                    // so include every potential destination in the portfolio bounds.
                    var roundingAllowance = (year.Accounts.Count + streams.Length) / 2m;
                    var fastestReturn = year.Accounts.Max(account => account.AppreciationRate);
                    if (fastestReturn <= maximumExpenseGrowth)
                    {
                        return false;
                    }

                    // Even crediting every dollar with the fastest return and
                    // favorable rounding cannot fund these future withdrawals.
                    var minimumRequired = streams.Sum(stream => stream.Amount / (fastestReturn - stream.Rate))
                        - roundingAllowance / fastestReturn;
                    if (year.EndingBalance < minimumRequired)
                    {
                        return false;
                    }

                    var slowestReturn = year.Accounts.Min(account => account.AppreciationRate);
                    if (slowestReturn > maximumExpenseGrowth)
                    {
                        var sufficientBalance = streams.Sum(stream => stream.Amount / (slowestReturn - stream.Rate))
                            + roundingAllowance / slowestReturn;
                        if (year.EndingBalance >= sufficientBalance)
                        {
                            return true;
                        }
                    }

                    foreach (var account in year.Accounts)
                    {
                        var mandatoryRate = Income.GetMandatoryRate(account.Type);
                        var rate = account.AppreciationRate - mandatoryRate;
                        if (rate <= maximumExpenseGrowth)
                        {
                            continue;
                        }

                        // Present value of every future withdrawal at this account's
                        // return after mandatory withdrawals, allowing $0.50/year
                        // per expense stream, growth, and mandatory rounding.
                        // This account alone can
                        // cover that liability; other accounts only add protection.
                        var requiredBalance = streams.Sum(stream => stream.Amount / (rate - stream.Rate))
                            + (streams.Length + 1m + (mandatoryRate > 0m ? 1m : 0m)) / (2m * rate);
                        if (account.EndingBalance >= requiredBalance)
                        {
                            return true;
                        }
                    }
                }

                year = PlanYear.ForecastFrom(year);
            }
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                "Perpetual sustainability could not be proved before forecast values overflowed.", exception);
        }

        throw new InvalidOperationException(
            $"Perpetual sustainability of {scenario.AnnualExpenses:C} could not be proved within "
            + $"{MaximumProofYears} forecast years. A long simulation is not proof of perpetuity.");
    }
}
