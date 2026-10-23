namespace FinPlanner.Engine;

public class Plan
{

    /// <summary>
    /// The calendar year at the start of the plan.
    /// </summary>
    public int BeginningYear { get; private set; }

    /// <summary>
    /// The calendar year at the end of the plan.
    /// </summary>
    public int EndingYear { get; private set; }

    /// <summary>
    /// The user's age at the start of the plan.
    /// </summary>
    public int BeginningAge { get; private set; }

    /// <summary>
    /// The user's age at the end of the plan.
    /// </summary>
    public int EndingAge { get; private set; }

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
    /// Creates the first plan year from the scenario.
    /// </summary>
    /// <param name="scenario"></param>
    public Plan(Scenario scenario)
    {
        BeginningYear = scenario.StartYear;
        EndingYear = scenario.EndYear;

        BeginningAge = scenario.CurrentAge;
        EndingAge = scenario.LifeExpectancy;

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
    /// </summary>
    public void Build()
    {
        IsSuccessful = false;

        if (PlanYears.Count == 0)
        {
            return;
        }

        FailureReason = null;

        // Remove any existing plan years beyond the first year, as we will rebuild the plan from scratch
        // The first year is already created in the constructor, so we only need to remove any additional years if they exist
        if (PlanYears.Count > 1)
        {
            PlanYears.RemoveRange(1, PlanYears.Count - 1);
        }

        var currentYear = PlanYears[0];

        while (true)
        {
            // Loop through all the accounts in the current year and appreciate their balances
            foreach (var account in currentYear.Accounts)
            {
                account.Appreciate();
            }

            var remainingExpenses = currentYear.ApplyExpenses();

            if (remainingExpenses > 0)
            {
                FailureReason = $"Plan fails at year {currentYear.EndingYear}.";
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
    /// Determines the maximum annual expenses that can be sustained over the plan's duration without depleting the accounts. It uses a MaximumExpenseCalculator to perform the calculation based on the initial scenario and the plan's parameters.
    /// </summary>
    /// <returns>The maximum annual expenses that can be sustained.</returns>
    public decimal GetMaximumAnnualExpenses()
    {
        decimal ExpenseStep = 1_000m;
        var trialMaximumExpenses = 0;

        var lowerBound = 0;
        var upperBound = Math.Max(
            1,
            (int)(PlanYears[0].Accounts.Sum(account => account.BeginningBalance)
                / ExpenseStep));

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + (upperBound - lowerBound) / 2;

            List<PlanYear> trialPlanYears = new List<PlanYear>();
            var currentYear = new PlanYear
            {
                EndingAge = BeginningAge,
                EndingYear = BeginningYear,
                Accounts = PlanYears[0].Accounts
                    .Select(account => new Account(account))
                    .ToList(),
                AnnualExpenses = midpoint * ExpenseStep
            };

            while (true)
            {
                foreach (var account in currentYear.Accounts)
                {
                    account.Appreciate();
                }

                trialPlanYears.Add(currentYear);

                if (currentYear.EndingYear >= EndingYear)
                {
                    break;
                }

                currentYear = PlanYear.ForecastFrom(currentYear);
            }

            var finalBalance = trialPlanYears[^1].EndingBalance;

            // A plan ending at exactly zero is considered unsuccessful.
            // We use decimal.Truncate to avoid floating-point precision issues.
            if (decimal.Truncate(finalBalance) > 0m)
            {
                trialMaximumExpenses = midpoint;
                lowerBound = midpoint + 1;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        return trialMaximumExpenses * ExpenseStep;
    }

}
