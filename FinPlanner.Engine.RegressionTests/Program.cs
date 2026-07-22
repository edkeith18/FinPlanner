using FinPlanner.Engine;

var failures = new List<string>();

Run("Legacy projection values", LegacyProjectionValues);
Run("Scenario remains unchanged", ScenarioRemainsUnchanged);
Run("Invalid age range returns no years", InvalidAgeRangeReturnsNoYears);
Run("Maximum expense excludes zero ending balance", MaximumExpenseExcludesZeroBalance);
Run("Withdrawal priorities follow account order", WithdrawalPrioritiesFollowAccountOrder);
Run("Account membership keeps priorities contiguous", AccountMembershipKeepsPrioritiesContiguous);
Run("Withdrawal priority JSON round trip", WithdrawalPriorityJsonRoundTrip);
Run("Legacy JSON normalizes withdrawal priorities", LegacyJsonNormalizesWithdrawalPriorities);
Run("Plan withdrawals use priority order", PlanWithdrawalsUsePriorityOrder);

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

void WithdrawalPrioritiesFollowAccountOrder()
{
    var scenario = CreateAccountScenario();

    Equal(1, scenario.Accounts[0].WithdrawalPriority, "first priority");
    Equal(2, scenario.Accounts[1].WithdrawalPriority, "second priority");
    Equal(3, scenario.Accounts[2].WithdrawalPriority, "third priority");

    var movedAccount = scenario.Accounts[2];
    scenario.Accounts.RemoveAt(2);
    scenario.Accounts.Insert(0, movedAccount);
    scenario.NormalizeWithdrawalPriorities();

    Equal("Third", scenario.Accounts[0].Name, "reordered first account");
    Equal(1, scenario.Accounts[0].WithdrawalPriority, "reordered first priority");
    Equal(2, scenario.Accounts[1].WithdrawalPriority, "reordered second priority");
    Equal(3, scenario.Accounts[2].WithdrawalPriority, "reordered third priority");
}

void AccountMembershipKeepsPrioritiesContiguous()
{
    var scenario = CreateAccountScenario();
    scenario.AddAccount("Fourth", 4_000m);
    Equal(4, scenario.Accounts[3].WithdrawalPriority, "added account priority");

    scenario.Accounts.RemoveAt(1);
    scenario.NormalizeWithdrawalPriorities();

    Equal(1, scenario.Accounts[0].WithdrawalPriority, "priority after deletion 1");
    Equal(2, scenario.Accounts[1].WithdrawalPriority, "priority after deletion 2");
    Equal(3, scenario.Accounts[2].WithdrawalPriority, "priority after deletion 3");
}

void WithdrawalPriorityJsonRoundTrip()
{
    var scenario = CreateAccountScenario();
    var json = scenario.Serialize();
    var roundTrip = Scenario.Deserialize(json);

    Equal(true, json.Contains("\"WithdrawalPriority\": 1"), "serialized priority");
    Equal(1, roundTrip.Accounts[0].WithdrawalPriority, "round-trip first priority");
    Equal(3, roundTrip.Accounts[2].WithdrawalPriority, "round-trip last priority");
}

void LegacyJsonNormalizesWithdrawalPriorities()
{
    const string json = """
        { "Accounts": [ { "Name": "First" }, { "Name": "Second" } ] }
        """;

    var scenario = Scenario.Deserialize(json);

    Equal(1, scenario.Accounts[0].WithdrawalPriority, "legacy first priority");
    Equal(2, scenario.Accounts[1].WithdrawalPriority, "legacy second priority");
}

void PlanWithdrawalsUsePriorityOrder()
{
    var scenario = CreateAccountScenario();
    scenario.StartYear = 2026;
    scenario.CurrentAge = 40;
    scenario.LifeExpectancy = 40;
    scenario.AnnualExpenses = 100m;
    var priorityAccountId = scenario.Accounts[1].Id;
    var priorityAccount = scenario.Accounts[1];
    scenario.Accounts.RemoveAt(1);
    scenario.Accounts.Insert(0, priorityAccount);
    scenario.NormalizeWithdrawalPriorities();

    var year = new PlanBuilder().Build(scenario).Years.Single();
    var priorityAccountResult = year.Accounts.Single(account => account.AccountId == priorityAccountId);

    Equal(100m, priorityAccountResult.ExpenseWithdrawals, "priority account withdrawal");
}

Scenario CreateAccountScenario()
{
    var scenario = new Scenario();
    scenario.AddAccount("First", 1_000m);
    scenario.AddAccount("Second", 2_000m);
    scenario.AddAccount("Third", 3_000m);
    return scenario;
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
