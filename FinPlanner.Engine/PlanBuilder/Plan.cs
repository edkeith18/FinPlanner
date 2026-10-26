namespace FinPlanner.Engine;

public class Plan
{
    private readonly Scenario scenario;

    public string Name { get; }

    /// <summary>
    /// The calendar year at the start of the plan.
    /// </summary>
    public int BeginningYear { get; private set; }

    /// <summary>
    /// The calendar year when the final 12-month period ends.
    /// </summary>
    public int EndingYear { get; private set; }

    /// <summary>
    /// Indicates whether or not plan was built successfully
    /// </summary>
    public bool IsSuccessful { get; private set; } = false;

    public string? FailureReason { get; private set; } = null;

    /// <summary>
    /// The PlanYears in the plan, in chronological order. Each PlanYear represents the financial results for a single 12-month period.
    /// </summary>
    public List<PlanYear> PlanYears { get; private set; } = new List<PlanYear>();

    /// <summary>
    /// Constructor
    /// Snapshots the scenario and creates the first plan year from those inputs.
    /// </summary>
    /// <param name="scenario"></param>
    /// <param name="name">The display name of the plan.</param>
    public Plan(Scenario scenario, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        this.scenario = new Scenario(scenario);
        scenario = this.scenario;
        BeginningYear = scenario.StartYear;
        EndingYear = scenario.EndYear;

        if (EndingYear <= BeginningYear)
        {
            FailureReason = "There are no remaining 12-month periods before life expectancy.";
            return;
        }

        var categorizedExpenseAmount =
            scenario.Expenses.Sum(expense => expense.Amount);

        var uncategorizedExpenseAmount =
            scenario.AnnualExpenses - categorizedExpenseAmount;

        if (uncategorizedExpenseAmount < 0)
        {
            FailureReason =
                "Known expenses exceed AnnualExpenses.";
            return;
        }

        PlanYears.Add(
            PlanYear.FromScenario(
                scenario,
                uncategorizedExpenseAmount));
    }

    /// <summary>
    /// Builds the plan by forecasting each year from the current year, starting from the beginning year to the ending year.
    /// Call once per Plan instance.
    /// </summary>
    public void Build()
    {
        IsSuccessful = false;

        if (PlanYears.Count == 0)
        {
            return;
        }

        FailureReason = null;

        var currentYear = PlanYears[0];

        while (true)
        {
            foreach (var account in currentYear.Accounts)
            {
                account.Appreciate();
            }

            var remainingExpenses = currentYear.ApplyExpenses();

            if (remainingExpenses > 0)
            {
                FailureReason = $"Plan fails at year {currentYear.StartingYear}.";
                break;
            }

            if (currentYear.EndingYear >= EndingYear)
            {
                IsSuccessful = true;
                break;
            }

            currentYear = PlanYear.ForecastFrom(currentYear);
            PlanYears.Add(currentYear);
        }
    }

    /// <summary>
    /// Finds the maximum sustainable initial annual expenses in $1,000 increments,
    /// keeping named expenses fixed and varying uncategorized expenses.
    /// </summary>
    public decimal GetMaximumAnnualExpenses()
        => GetMaximumAnnualExpenses(out _);

    /// <summary>
    /// Also returns the successful trial plan for the maximum expense amount.
    /// The plan is null if no sustainable candidate was found.
    /// </summary>
    public decimal GetMaximumAnnualExpenses(out Plan? maximumSpendPlan)
    {
        maximumSpendPlan = null;
        Plan? bestPlan = null;
        const decimal ExpenseStep = 1_000m;
        if (this.scenario.EndYear <= this.scenario.StartYear)
        {
            return 0m;
        }

        var scenario = new Scenario(this.scenario);

        bool CanSustain(decimal expenseSteps)
        {
            scenario.AnnualExpenses = expenseSteps * ExpenseStep;
            var trialPlan = new Plan(scenario, "Maximum Spend Plan");
            trialPlan.Build();

            // Preserve the requirement that some money remains at the end.
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
        var maximumExpenses = 0m;

        // Find a failing upper bound, including when growth supports spending above initial assets.
        while (CanSustain(upperBound))
        {
            maximumExpenses = upperBound * ExpenseStep;
            lowerBound = upperBound + 1;
            upperBound *= 2;
        }

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + decimal.Floor((upperBound - lowerBound) / 2);
            if (CanSustain(midpoint))
            {
                maximumExpenses = midpoint * ExpenseStep;
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        maximumSpendPlan = bestPlan;
        return maximumExpenses;
    }

}
