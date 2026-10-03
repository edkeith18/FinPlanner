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

    private PlanYear PreviousYear;

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
    /// Makes a "YearZero" based on the scenario.  It is used as a seed for building the plan.
    /// </summary>
    /// <param name="scenario"></param>
    public Plan(Scenario scenario)
    {
        BeginningYear = scenario.StartYear;
        EndingYear = scenario.EndYear;

        BeginningAge = scenario.CurrentAge;
        EndingAge = scenario.CurrentAge + scenario.EndYear - scenario.StartYear;

        // Seed PreviousYear's ending values with the scenario's starting values
        PreviousYear = new PlanYear
        {
            EndingAge = this.BeginningAge,
            EndingYear = this.BeginningYear,
            Accounts = scenario.Accounts.Select(account => new Account(account)).ToList(),
            AnnualExpenses = scenario.AnnualExpenses
        };
    }

    /// <summary>
    /// Builds the plan by forecasting each year from the previous year, starting from the beginning year to the ending year.
    /// </summary>
    public void Build()
    {
        for (int year = BeginningYear; year <= EndingYear; year++)
        {
            PlanYears.Add(PlanYear.ForecastFrom(PreviousYear));
            PreviousYear = PlanYears[^1];
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
            (int)(PreviousYear.Accounts.Sum(account => account.EndingBalance)
                / ExpenseStep));

        while (lowerBound <= upperBound)
        {
            var midpoint = lowerBound + (upperBound - lowerBound) / 2;

            // Local variables to hold the plan years as we iterate through the binary search
            List<PlanYear> trialPlanYears = new List<PlanYear>();
            PlanYear trialPreviousYear = new PlanYear()
            {
                EndingAge = this.BeginningAge,
                EndingYear = this.BeginningYear,
                Accounts = PreviousYear.Accounts.Select(account => new Account(account)).ToList(),
                AnnualExpenses = midpoint * ExpenseStep
            };

            for (int year = BeginningYear; year <= EndingYear; year++)
            {
                trialPlanYears.Add(PlanYear.ForecastFrom(trialPreviousYear));
                trialPreviousYear = trialPlanYears[^1];
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
