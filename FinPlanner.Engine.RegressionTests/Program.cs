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
Run("Withdrawals move to next account", WithdrawalsMoveToNextAccount);
Run("Exhausted balances fail and stop plan", ExhaustedBalancesFailAndStopPlan);
Run("Single account withdrawals remain intact", SingleAccountWithdrawalsRemainIntact);
Run("Age calculation around birthdays", AgeCalculationAroundBirthdays);
Run("Leap-day age calculation", LeapDayAgeCalculation);
Run("Date of birth exact parsing", DateOfBirthExactParsing);
Run("Date of birth rejects invalid values", DateOfBirthRejectsInvalidValues);
Run("Date of birth JSON behavior", DateOfBirthJsonBehavior);
Run("Async deserialization derives current age", AsyncDeserializationDerivesCurrentAge);
Run("Derived age drives plan calculations", DerivedAgeDrivesPlanCalculations);
Run("Age ranges remain unchanged", AgeRangesRemainUnchanged);

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
    scenario.DateOfBirth = DateOnly.FromDateTime(DateTime.Now).AddYears(-80);
    scenario.LifeExpectancy = 79;

    var plan = new PlanBuilder().Build(scenario);

    Equal(0, plan.Years.Count, "year count");
}

void MaximumExpenseExcludesZeroBalance()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        DateOfBirth = DateOfBirthForAge(40),
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
    scenario.DateOfBirth = DateOfBirthForAge(40);
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
    Equal(0m, year.Accounts.Single(account => account.AccountName == "First").ExpenseWithdrawals, "lower-priority first account withdrawal");
    Equal(0m, year.Accounts.Single(account => account.AccountName == "Third").ExpenseWithdrawals, "lower-priority third account withdrawal");
}

void WithdrawalsMoveToNextAccount()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        DateOfBirth = DateOfBirthForAge(40),
        LifeExpectancy = 40,
        AnnualExpenses = 150m
    };
    scenario.AddAccount("First", 100m);
    scenario.AddAccount("Second", 100m);

    var plan = new PlanBuilder().Build(scenario);
    var year = plan.Years.Single();

    Equal(100m, year.Accounts[0].ExpenseWithdrawals, "first account withdrawal");
    Equal(0m, year.Accounts[0].EndingBalance, "first account ending balance");
    Equal(50m, year.Accounts[1].ExpenseWithdrawals, "second account withdrawal");
    Equal(50m, year.Accounts[1].EndingBalance, "second account ending balance");
    Equal(true, plan.IsSuccessful, "plan success");
}

void ExhaustedBalancesFailAndStopPlan()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        DateOfBirth = DateOfBirthForAge(40),
        LifeExpectancy = 42,
        AnnualExpenses = 100m
    };
    scenario.AddAccount("Brokerage", 100m);

    var plan = new PlanBuilder().Build(scenario);

    Equal(false, plan.IsSuccessful, "plan success");
    Equal(true, plan.FailureReason is not null, "failure reason");
    Equal(1, plan.Years.Count, "calculated year count");
    Equal(2026, plan.LastYear!.CalendarYear, "last calculated year");
    Equal(0m, plan.LastYear.EndingBalance, "last ending balance");
}

void SingleAccountWithdrawalsRemainIntact()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        DateOfBirth = DateOfBirthForAge(40),
        LifeExpectancy = 40,
        AnnualExpenses = 100m
    };
    scenario.AddAccount("Brokerage", 1_000m);

    var plan = new PlanBuilder().Build(scenario);
    var account = plan.Years.Single().Accounts.Single();

    Equal(100m, account.ExpenseWithdrawals, "expense withdrawal");
    Equal(900m, account.EndingBalance, "ending balance");
    Equal(true, plan.IsSuccessful, "plan success");
}

void AgeCalculationAroundBirthdays()
{
    var asOf = new DateOnly(2026, 7, 25);

    Equal(68, Scenario.CalculateAge(new DateOnly(1958, 7, 24), asOf), "age after birthday");
    Equal(67, Scenario.CalculateAge(new DateOnly(1958, 7, 26), asOf), "age before birthday");
    Equal(68, Scenario.CalculateAge(new DateOnly(1958, 7, 25), asOf), "age on birthday");
}

void LeapDayAgeCalculation()
{
    var dateOfBirth = new DateOnly(2000, 2, 29);

    // DateOnly.AddYears treats February 28 as the anniversary in non-leap years.
    Equal(25, Scenario.CalculateAge(dateOfBirth, new DateOnly(2025, 2, 28)), "leap-day age on February 28");
    Equal(25, Scenario.CalculateAge(dateOfBirth, new DateOnly(2025, 3, 1)), "leap-day age after February 28");
}

void DateOfBirthExactParsing()
{
    var today = new DateOnly(2026, 7, 25);

    Equal(true, Scenario.TryParseDateOfBirth("07/24/58", today, out var shortYear, out _), "two-digit parse");
    Equal(new DateOnly(1958, 7, 24), shortYear, "two-digit interpretation");
    Equal(true, Scenario.TryParseDateOfBirth("07/24/2005", today, out var fullYear, out _), "four-digit parse");
    Equal(new DateOnly(2005, 7, 24), fullYear, "four-digit interpretation");
    Equal(true, Scenario.TryParseDateOfBirth("07/24/26", today, out var currentCentury, out _), "current-century parse");
    Equal(new DateOnly(2026, 7, 24), currentCentury, "current-century interpretation");
}

void DateOfBirthRejectsInvalidValues()
{
    var today = new DateOnly(2026, 7, 25);

    Equal(false, Scenario.TryParseDateOfBirth("7/24/1958", today, out _, out _), "non-exact format");
    Equal(false, Scenario.TryParseDateOfBirth("02/30/1965", today, out _, out _), "impossible date");
    Equal(false, Scenario.TryParseDateOfBirth("07/26/2026", today, out _, out _), "future date");
    Equal(false, Scenario.TryParseDateOfBirth("07/26/26", today, out _, out _), "future short-year date");
}

void DateOfBirthJsonBehavior()
{
    var scenario = new Scenario { DateOfBirth = new DateOnly(1958, 7, 24) };
    var json = scenario.Serialize();
    var roundTrip = Scenario.Deserialize(json);

    Equal(true, json.Contains("\"DateOfBirth\": \"07/24/1958\""), "serialized date of birth");
    Equal(false, json.Contains("CurrentAge"), "excluded current age");
    Equal(scenario.DateOfBirth, roundTrip.DateOfBirth, "round-trip date of birth");
    Equal(Scenario.CalculateAge(roundTrip.DateOfBirth, DateOnly.FromDateTime(DateTime.Now)), roundTrip.CurrentAge, "derived current age");

    AssertThrowsJson("{ \"DateOfBirth\": \"02/30/1965\" }", "impossible JSON date");
    AssertThrowsJson($"{{ \"DateOfBirth\": \"{DateOnly.FromDateTime(DateTime.Now).AddDays(1):MM/dd/yyyy}\" }}", "future JSON date");
}

void AsyncDeserializationDerivesCurrentAge()
{
    const string json = "{ \"DateOfBirth\": \"07/24/1958\" }";
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    var scenario = Scenario.DeserializeAsync(stream).GetAwaiter().GetResult();

    Equal(Scenario.CalculateAge(scenario.DateOfBirth, DateOnly.FromDateTime(DateTime.Now)), scenario.CurrentAge, "async derived age");
}

void DerivedAgeDrivesPlanCalculations()
{
    var scenario = new Scenario
    {
        StartYear = 2026,
        DateOfBirth = DateOfBirthForAge(40),
        LifeExpectancy = 42
    };

    var plan = new PlanBuilder().Build(scenario);

    Equal(3, plan.Years.Count, "derived-age year count");
    Equal(40, plan.Years[0].Age, "derived starting age");
    Equal(42, plan.Years[^1].Age, "life-expectancy ending age");
}

void AgeRangesRemainUnchanged()
{
    var scenario = new Scenario { DateOfBirth = DateOfBirthForAge(40) };
    scenario.AddExpense("Range", 1m, 41, 42, 0m);
    scenario.AddTransfer("Range", 1m, 43, 44, "A", "B", 0m);

    Equal(41, scenario.Expenses[0].AgeStart, "expense AgeStart");
    Equal(42, scenario.Expenses[0].AgeEnd, "expense AgeEnd");
    Equal(43, scenario.Transfers[0].AgeStart, "transfer AgeStart");
    Equal(44, scenario.Transfers[0].AgeEnd, "transfer AgeEnd");
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
        DateOfBirth = DateOfBirthForAge(40),
        LifeExpectancy = 42,
        AnnualExpenses = 20_000m,
        AnnualInflationRate = 5m,
        SecuritiesAnnualRateOfReturn = 10m
    };
    scenario.AddAccount("Brokerage", 100_000m);
    scenario.AddExpense("Housing", 5_000m, 40, 42, 10m);
    return scenario;
}

DateOnly DateOfBirthForAge(int age)
{
    return DateOnly.FromDateTime(DateTime.Now).AddYears(-age);
}

void AssertThrowsJson(string json, string description)
{
    try
    {
        _ = Scenario.Deserialize(json);
    }
    catch (System.Text.Json.JsonException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {description} to throw JsonException.");
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
