using FinPlanner.Engine;

var failures = new List<string>();

Run("Legacy projection values", LegacyProjectionValues);
Run("Scenario remains unchanged", ScenarioRemainsUnchanged);
Run("Invalid age range returns no years", InvalidAgeRangeReturnsNoYears);
Run("Maximum expense excludes zero ending balance", MaximumExpenseExcludesZeroBalance);

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("All PlanBuilder regression checks passed.");
return 0;

void Run(string name, Action test)
{
    try
    {
        test();
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
    }
}

void LegacyProjectionValues()
{
    var plan = new PlanBuilder().Build(CreateProjectionScenario());

    Equal(3, plan.Years.Count, "year count");
    AssertYear(plan.Years[0], 40, 20_000m, 80_000m);
    AssertYear(plan.Years[1], 41, 21_250m, 66_750m);
    AssertYear(plan.Years[2], 42, 22_587.50m, 50_837.50m);
}

void ScenarioRemainsUnchanged()
{
    var scenario = CreateProjectionScenario();
    _ = new PlanBuilder().Build(scenario);

    Equal(100_000m, scenario.Accounts[0].Balance, "scenario account balance");
    Equal(5_000m, scenario.Expenses[0].Amount, "scenario expense amount");
}

void InvalidAgeRangeReturnsNoYears()
{
    var scenario = CreateProjectionScenario();
    scenario.CurrentAge = 80;
    scenario.LifeExpectancy = 79;

    var plan = new PlanBuilder().Build(scenario);

    Equal(0, plan.Years.Count, "year count");
}

void MaximumExpenseExcludesZeroBalance()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        CurrentAge = 40,
        LifeExpectancy = 40
    };
    scenario.AddAccount("Brokerage", 10_000m);

    var calculator = new MaximumExpenseCalculator(new PlanBuilder());
    var succeeded = calculator.TryCalculate(scenario, out var maximum);

    Equal(true, succeeded, "calculation success");
    Equal(9_000m, maximum, "maximum expenses");
}

Scenario CreateProjectionScenario()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        CurrentAge = 40,
        LifeExpectancy = 42,
        AnnualExpenses = 20_000m,
        AnnualInflationRate = 5m,
        SecuritiesAnnualInterestRate = 10m
    };
    scenario.AddAccount("Brokerage", 100_000m);
    scenario.AddExpense("Housing", 5_000m, 40, 42, 10m);
    return scenario;
}

void AssertYear(
    PlanYear year,
    int age,
    decimal totalExpenses,
    decimal endingBalance)
{
    Equal(age, year.Age, $"age for {year.CalendarYear}");
    Equal(totalExpenses, year.TotalExpenses, $"expenses for {year.CalendarYear}");
    Equal(endingBalance, year.EndingBalance, $"balance for {year.CalendarYear}");
}

void Equal<T>(T expected, T actual, string description)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Expected {description} to be {expected}, but it was {actual}.");
    }
}
