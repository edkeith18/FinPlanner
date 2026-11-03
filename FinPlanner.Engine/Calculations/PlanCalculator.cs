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
    /// Calculates a plan whose initial annual expenses can be sustained indefinitely.
    /// Lifetime named expenses continue at their own growth rates; uncategorized
    /// expenses grow with inflation forever. The returned years cover the scenario's
    /// original lifetime for reporting. Uses conservative tax bounds; a successful
    /// result certifies a budget without proving the exact maximum.
    /// </summary>
    /// <exception cref="NotSupportedException">An input balance, expense, income, or rate is negative.</exception>
    public Plan CalculatePerpetualSpend(Scenario scenario)
    {
        scenario = new Scenario(scenario);
        const string Name = "Perpetual Spend Plan";
        return CalculateTaxedPerpetualSpend(scenario, Name);
    }

    private Plan CalculateTaxedPerpetualSpend(Scenario scenario, string name)
    {
        ValidateTaxScenario(scenario);
        if (scenario.EndYear <= scenario.StartYear) return Build(scenario, name);
        var proofScenario = new Scenario(scenario);
        foreach (var expense in proofScenario.Expenses.Where(expense => expense.AgeEnd >= scenario.LifeExpectancy))
            expense.AgeEnd = int.MaxValue;
        proofScenario.LifeExpectancy = int.MaxValue;
        var named = StartingNamedExpenses(proofScenario);
        var lower = Math.Max(0m, decimal.Ceiling((named + scenario.FirstYearTaxExpense) / ExpenseStep));
        var upper = Math.Max(lower, Math.Max(1m, decimal.Ceiling(
            scenario.Accounts.Sum(account => account.BeginningBalance) / ExpenseStep)));
        decimal? best = null;
        bool Certify(decimal steps)
        {
            proofScenario.FirstYearAnnualExpenses = steps * ExpenseStep;
            if (!CertifyTaxedBudget(proofScenario, proofScenario.FirstYearAnnualExpenses - proofScenario.FirstYearTaxExpense - named)) return false;
            best = proofScenario.FirstYearAnnualExpenses;
            return true;
        }
        while (Certify(upper)) { lower = upper + 1; upper *= 2; }
        while (lower <= upper)
        {
            var middle = lower + decimal.Floor((upper - lower) / 2);
            if (Certify(middle)) lower = middle + 1;
            else upper = middle - 1;
        }
        const string note = "Tax-aware perpetual spending uses conservative tax bounds. This budget is certified; the exact maximum is not proven.";
        if (best is null)
            return new Plan(scenario, name, [], false,
                "No perpetual budget could be certified with the tax model; this does not prove that no sustainable budget exists.");
        scenario.FirstYearAnnualExpenses = best.Value;
        var plan = Build(scenario, name);
        return new Plan(scenario, name, plan.PlanYears.ToList(), plan.IsSuccessful, plan.FailureReason, note);
    }

    private static void ValidateTaxScenario(Scenario scenario)
    {
        if (scenario.AnnualInflationRate < 0m || scenario.SecuritiesAnnualRateOfReturn < 0m
            || scenario.BondsAnnualRateOfReturn < 0m || scenario.Accounts.Any(account => account.BeginningBalance < 0m)
            || scenario.Expenses.Any(expense => expense.Amount < 0m || expense.AnnualRateOfIncrease < 0m)
            || scenario.Income.Any(income => income.InitialAmount < 0m || income.AnnualRateOfIncrease < 0m)
            || scenario.Transfers.Any(transfer => transfer.Amount < 0m || transfer.AnnualRateOfIncrease < 0m))
            throw new NotSupportedException("Tax-aware perpetual proofs require nonnegative balances, amounts, returns, and growth rates.");
    }

    private static bool CertifyTaxedBudget(Scenario scenario, decimal uncategorized)
    {
        var lastTransition = scenario.Expenses.Where(expense => expense.Amount > 0m)
            .Select(expense => expense.AgeEnd == int.MaxValue ? expense.AgeStart - 1 : expense.AgeEnd)
            .Concat(scenario.Income.Where(income => income.InitialAmount > 0m).Select(income => income.AgeStart - 1))
            .Concat(scenario.Transfers.Where(transfer => transfer.Amount > 0m && transfer.AgeStart <= transfer.AgeEnd)
                .Select(transfer => transfer.AgeEnd))
            .DefaultIfEmpty(scenario.CurrentAge - 1).Max();
        var year = PlanYearCalculator.FromScenario(scenario, uncategorized);
        var net = 1m - scenario.TaxInfo.MaximumIncomeTaxRate;
        try
        {
            while (year.YearsInFuture < MaximumProofYears)
            {
                PlanYearCalculator.Calculate(year, scenario.TaxInfo);
                if (!year.IsFullyFunded) return false;
                if (year.YearsInFuture == 0) uncategorized = year.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount;
                if (year.StartingAge > lastTransition
                    && year.Accounts.All(account => account.EndingBalance == 0m
                        || RmdCalculator.IsScheduleStable(account.Type, year.StartingAge)))
                {
                    var streams = scenario.Expenses.Where(expense => expense.AgeEnd == int.MaxValue)
                        .Select(expense => (Amount: expense.Amount * TaxCalculator.Growth(expense.AnnualRateOfIncrease,
                            year.EndingAge - scenario.CurrentAge) / net, Rate: expense.AnnualRateOfIncrease)).ToList();
                    streams.Add((uncategorized * TaxCalculator.Growth(scenario.AnnualInflationRate / 100m,
                        year.EndingAge - scenario.CurrentAge) / net, scenario.AnnualInflationRate / 100m));
                    streams.RemoveAll(stream => stream.Amount <= 0m);
                    var destination = year.Accounts.Where(account => account.Type == AccountType.Brokerage)
                        .OrderBy(account => account.WithdrawalPriority).FirstOrDefault();
                    var relevant = year.Accounts.Where(account => account.EndingBalance > 0m || account == destination).ToArray();
                    // All earnings and mandatory distributions can be taxed at the
                    // envelope rate. Additional withdrawals are grossed up above.
                    var minimumReturn = relevant.Select(account => account.AppreciationRate
                        + PlanYearCalculator.GetEarnedRate(account) * net
                        - RmdCalculator.GetMandatoryRate(account, year.StartingAge) * scenario.TaxInfo.MaximumIncomeTaxRate)
                        .DefaultIfEmpty(0m).Min();
                    var rounding = (year.Accounts.Count * 3m + streams.Count + 1m) / net;
                    bool Covers(decimal balance, decimal rate) => rate > 0m
                        && streams.All(stream => stream.Rate < rate)
                        && balance >= streams.Sum(stream => stream.Amount / (rate - stream.Rate)) + rounding / rate;
                    // A brokerage destination is required for any future surplus.
                    if (destination is not null && Covers(year.EndingBalance, minimumReturn)) return true;
                    foreach (var account in year.Accounts)
                    {
                        var retained = account.AppreciationRate - RmdCalculator.GetMandatoryRate(account, year.StartingAge);
                        if (destination is not null && Covers(account.EndingBalance, retained)) return true;
                    }
                }
                year = PlanYearCalculator.ForecastFrom(year, scenario, uncategorized);
            }
        }
        catch (OverflowException) { /* No certificate: never accept overflow as sustainability. */ }
        return false;
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
