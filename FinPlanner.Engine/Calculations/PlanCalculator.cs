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
            StartingNamedExpenses(scenario) / ExpenseStep));
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
    /// <exception cref="NotSupportedException">An input balance, expense, income, or rate is negative.</exception>
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
            - StartingNamedExpenses(scenario);
        if (uncategorizedExpenses < 0m)
        {
            return new Plan(scenario, name, years, false, "Known expenses exceed AnnualExpenses.");
        }

        var currentYear = PlanYearCalculator.FromScenario(scenario, uncategorizedExpenses);
        while (true)
        {
            years.Add(currentYear);
            PlanYearCalculator.Calculate(currentYear);
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

    private static decimal? FindPerpetualAnnualExpenses(Scenario scenario)
    {
        if (scenario.AnnualInflationRate < 0m
            || scenario.SecuritiesAnnualRateOfReturn < 0m
            || scenario.BondsAnnualRateOfReturn < 0m
            || scenario.Accounts.Any(account => account.BeginningBalance < 0m)
            || scenario.Expenses.Any(expense => expense.Amount < 0m || expense.AnnualRateOfIncrease < 0m)
            || scenario.Income.Any(income => income.InitialAmount < 0m || income.AnnualRateOfIncrease < 0m)
            || scenario.Transfers.Any(transfer => transfer.Amount < 0m || transfer.AnnualRateOfIncrease < 0m))
        {
            throw new NotSupportedException(
                "The perpetual calculation requires nonnegative balances, expenses, income, returns, and inflation.");
        }

        // Preserve the original lifetime boundary when identifying ongoing expenses.
        foreach (var expense in scenario.Expenses.Where(
            expense => expense.AgeEnd >= scenario.LifeExpectancy))
        {
            expense.AgeEnd = int.MaxValue;
        }

        // Life expectancy limits the report, not ongoing expense streams.
        scenario.LifeExpectancy = int.MaxValue;
        var namedExpenses = StartingNamedExpenses(scenario);
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

    // AnnualExpenses describes the starting-period budget. Future and expired
    // named expenses must not reduce its uncategorized portion.
    private static decimal StartingNamedExpenses(Scenario scenario)
        => scenario.Expenses.Where(expense => expense.IsActiveAtAge(scenario.CurrentAge))
            .Sum(expense => expense.Amount);

    // After scheduled transfers finish, an empty account stays empty unless it
    // is the surplus destination. Include that destination even when empty:
    // future reinvestments can expose the portfolio to its return.
    private static decimal GetMinimumFutureReturn(PlanYear year)
    {
        var surplusDestination = year.Accounts.Where(account => account.Type == AccountType.Brokerage)
            .OrderBy(account => account.WithdrawalPriority).FirstOrDefault();
        return year.Accounts.Where(account => account.EndingBalance > 0m || account == surplusDestination)
            .Select(PlanYearCalculator.GetTotalReturn).DefaultIfEmpty(0m).Min();
    }

    // External income changes the future funding obligation. Use discounted bounds
    // only after all starts/ends and RMD transitions, retaining actual annual simulation
    // until a sufficient or necessary bound proves the candidate.
    private static bool CanSustainPerpetuallyWithIncome(Scenario scenario, decimal uncategorizedExpenses)
    {
        var year = PlanYearCalculator.FromScenario(scenario, uncategorizedExpenses);
        var expenses = scenario.Expenses.Where(expense => expense.AgeEnd == int.MaxValue)
            .Select(expense => (Amount: expense.Amount, Rate: expense.AnnualRateOfIncrease))
            .Append((Amount: uncategorizedExpenses, Rate: scenario.AnnualInflationRate / 100m))
            .Where(stream => stream.Amount > 0m).ToArray();
        var incomes = scenario.Income.Where(income => income.InitialAmount > 0m)
            .Select(income => (Amount: income.InitialAmount, Rate: income.AnnualRateOfIncrease)).ToArray();
        // Constant streams can be rounded exactly once; growing streams require
        // an allowance for each component's independent whole-dollar rounding.
        for (var i = 0; i < expenses.Length; i++)
            if (expenses[i].Rate == 0m) expenses[i].Amount = decimal.Round(expenses[i].Amount, 0, MidpointRounding.AwayFromZero);
        for (var i = 0; i < incomes.Length; i++)
            if (incomes[i].Rate == 0m) incomes[i].Amount = decimal.Round(incomes[i].Amount, 0, MidpointRounding.AwayFromZero);

        var lastTransitionAge = scenario.Expenses.Where(expense => expense.Amount > 0m)
            .Select(expense => expense.AgeEnd == int.MaxValue ? expense.AgeStart - 1 : expense.AgeEnd)
            .Concat(scenario.Income.Where(income => income.InitialAmount > 0m).Select(income => income.AgeStart - 1))
            .Concat(scenario.Transfers.Where(transfer => transfer.Amount > 0m
                && transfer.AgeStart <= transfer.AgeEnd && transfer.AgeEnd >= scenario.CurrentAge)
                .Select(transfer => transfer.AgeEnd))
            .DefaultIfEmpty(scenario.CurrentAge - 1).Max();
        var hasBrokerage = year.Accounts.Any(account => account.Type == AccountType.Brokerage);
        var constantCash = expenses.All(stream => stream.Rate == 0m) && incomes.All(stream => stream.Rate == 0m);

        try
        {
            for (var index = 0; index < MaximumProofYears; index++)
            {
                PlanYearCalculator.Calculate(year);
                if (!year.IsFullyFunded) return false;
                for (var i = 0; i < expenses.Length; i++) expenses[i].Amount *= 1m + expenses[i].Rate;
                for (var i = 0; i < incomes.Length; i++) incomes[i].Amount *= 1m + incomes[i].Rate;

                if (year.StartingAge > lastTransitionAge
                    && year.Accounts.All(account => account.EndingBalance == 0m
                        || RmdCalculator.IsScheduleStable(account.Type, year.StartingAge)))
                {
                    // One income stream covering one expense stream needs no
                    // rounding allowance: rounding preserves their ordering.
                    var unmatchedIncome = incomes.ToList();
                    var individuallyCovered = true;
                    foreach (var expense in expenses.OrderByDescending(stream => stream.Rate)
                        .ThenByDescending(stream => stream.Amount))
                    {
                        var match = unmatchedIncome.FindIndex(income => income.Rate >= expense.Rate
                            && income.Amount >= expense.Amount);
                        if (match < 0) { individuallyCovered = false; break; }
                        unmatchedIncome.RemoveAt(match);
                    }
                    var identicalCash = expenses.OrderBy(stream => stream.Rate).ThenBy(stream => stream.Amount)
                        .SequenceEqual(incomes.OrderBy(stream => stream.Rate).ThenBy(stream => stream.Amount));
                    if (individuallyCovered && (hasBrokerage || (identicalCash
                        && year.Accounts.All(account => account.EndingBalance == 0m
                            || account.Type is AccountType.Hsa or AccountType.RetirementRoth))))
                        return true;

                    var rounding = (expenses.Count(stream => stream.Rate > 0m)
                        + incomes.Count(stream => stream.Rate > 0m)
                        + year.Accounts.Count
                        + year.Accounts.Sum(account => PlanYearCalculator.HasBrokerageEquityIncome(account) ? 3 : 1)) / 2m;
                    var fastest = year.Accounts.Select(PlanYearCalculator.GetTotalReturn).DefaultIfEmpty(0m).Max();
                    var net = expenses.Concat(incomes.Select(stream => (Amount: -stream.Amount, stream.Rate)))
                        .GroupBy(stream => stream.Rate)
                        .Select(group => (Amount: group.Sum(stream => stream.Amount), Rate: group.Key))
                        .Where(stream => stream.Amount != 0m).OrderByDescending(stream => stream.Rate).ToArray();
                    // A dominant expense stream that outruns every asset cannot be
                    // supported forever by slower income streams.
                    if (net.Length > 0 && net[0].Amount > 0m && net[0].Rate >= fastest)
                        return false;
                    if (fastest > 0m && net.All(stream => stream.Rate < fastest))
                    {
                        var necessary = net.Sum(stream => stream.Amount / (fastest - stream.Rate)) - rounding / fastest;
                        if (year.EndingBalance < necessary) return false;
                    }

                    // Match income only to expenses growing no faster than that
                    // income. The residual is an upper bound on future withdrawals;
                    // unused income is conservatively ignored by this certificate.
                    var residual = expenses.Select(stream =>
                            (Amount: stream.Amount + (stream.Rate > 0m ? 0.5m : 0m), stream.Rate))
                        .OrderByDescending(stream => stream.Rate).ToArray();
                    foreach (var income in incomes.OrderBy(stream => stream.Rate))
                    {
                        var available = Math.Max(0m, income.Amount - (income.Rate > 0m ? 0.5m : 0m));
                        for (var i = 0; i < residual.Length && available > 0m; i++)
                        {
                            if (residual[i].Rate > income.Rate) continue;
                            var matched = Math.Min(available, residual[i].Amount);
                            residual[i].Amount -= matched;
                            available -= matched;
                        }
                    }
                    var remaining = residual.Where(stream => stream.Amount > 0m).ToArray();
                    // Without a surplus account, certify only constant external cash
                    // and accounts that cannot introduce future mandatory surplus.
                    var canReinvest = hasBrokerage || (constantCash
                        && incomes.Sum(stream => stream.Amount) <= expenses.Sum(stream => stream.Amount)
                        && year.Accounts.All(account => account.EndingBalance == 0m
                            || account.Type is AccountType.Hsa or AccountType.RetirementRoth));
                    if (canReinvest)
                    {
                        // The matching includes worst-case independent rounding.
                        // If income covers every stream now and grows at least as
                        // fast as its matched expense, no account funding is needed.
                        if (remaining.Length == 0) return true;
                        if (constantCash && year.Accounts.All(account => account.EndingBalance >= account.BeginningBalance))
                            return true;
                        var slowest = GetMinimumFutureReturn(year);
                        if (slowest > 0m && remaining.All(stream => stream.Rate < slowest))
                        {
                            var sufficient = remaining.Sum(stream => stream.Amount / (slowest - stream.Rate)) + rounding / slowest;
                            if (year.EndingBalance >= sufficient) return true;
                        }
                        foreach (var account in year.Accounts)
                        {
                            var retainedReturn = PlanYearCalculator.GetTotalReturn(account)
                                - PlanYearCalculator.GetEarnedRate(account)
                                - RmdCalculator.GetMandatoryRate(account, year.StartingAge);
                            if (retainedReturn <= 0m || remaining.Any(stream => stream.Rate >= retainedReturn)) continue;
                            var sufficient = remaining.Sum(stream => stream.Amount / (retainedReturn - stream.Rate))
                                + rounding / retainedReturn;
                            if (account.EndingBalance >= sufficient) return true;
                        }
                    }
                }
                year = PlanYearCalculator.ForecastFrom(year, scenario, uncategorizedExpenses);
            }
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Perpetual income sustainability could not be proved before forecast values overflowed.", exception);
        }
        throw new InvalidOperationException(
            $"Perpetual sustainability with income could not be proved within {MaximumProofYears} forecast years.");
    }

    private static bool CanSustainPerpetually(Scenario scenario, decimal uncategorizedExpenses)
    {
        if (scenario.Income.Any(income => income.InitialAmount > 0m))
            return CanSustainPerpetuallyWithIncome(scenario, uncategorizedExpenses);

        var year = PlanYearCalculator.FromScenario(scenario, uncategorizedExpenses);
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
        var maximumReturn = year.Accounts.Max(PlanYearCalculator.GetTotalReturn);
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
            .Concat(scenario.Transfers.Where(transfer => transfer.Amount > 0m
                && transfer.AgeStart <= transfer.AgeEnd && transfer.AgeEnd >= scenario.CurrentAge)
                .Select(transfer => transfer.AgeEnd))
            .DefaultIfEmpty(scenario.CurrentAge - 1)
            .Max();

        try
        {
            for (var index = 0; index < MaximumProofYears; index++)
            {
                PlanYearCalculator.Calculate(year);
                if (!year.IsFullyFunded || year.EndingBalance <= 0m)
                {
                    return false;
                }

                for (var streamIndex = 0; streamIndex < streams.Length; streamIndex++)
                {
                    streams[streamIndex].Amount *= 1m + streams[streamIndex].Rate;
                }
                // Wait out future RMD starts and divisor changes before using
                // proofs that assume the same annual rules continue forever.
                if (year.StartingAge > lastTransitionAge
                    && year.Accounts.All(account => account.EndingBalance == 0m
                        || RmdCalculator.IsScheduleStable(account.Type, year.StartingAge))
                    && (hasSurplusDestination || year.Accounts.All(account =>
                        account.Type is not (AccountType.Inherited or AccountType.Retirement)
                        || account.EndingBalance == 0m)))
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
                    // Brokerage has three separately rounded income components.
                    var incomeRounding = year.Accounts.Sum(account =>
                        PlanYearCalculator.HasBrokerageEquityIncome(account) ? 3m : 1m);
                    var roundingAllowance = (year.Accounts.Count + streams.Length + incomeRounding) / 2m;
                    var fastestReturn = year.Accounts.Max(PlanYearCalculator.GetTotalReturn);
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

                    var slowestReturn = GetMinimumFutureReturn(year);
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
                        var mandatoryRate = PlanYearCalculator.GetEarnedRate(account)
                            + RmdCalculator.GetMandatoryRate(account, year.StartingAge);
                        var rate = PlanYearCalculator.GetTotalReturn(account) - mandatoryRate;
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
                            + (streams.Length + 1m + (PlanYearCalculator.HasBrokerageEquityIncome(account)
                                ? 3m : mandatoryRate > 0m ? 1m : 0m)) / (2m * rate);
                        if (account.EndingBalance >= requiredBalance)
                        {
                            return true;
                        }
                    }
                }

                year = PlanYearCalculator.ForecastFrom(year, scenario, uncategorizedExpenses);
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
