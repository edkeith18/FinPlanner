using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ExpenseForecastTests
{
    [Fact]
    public void CurrentTrajectoryAppreciatesBalancesBeforeExpensesAndPreservesStartingAge()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 100m,
            SecuritiesAnnualRateOfReturn = 0.10m,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 1000m }]
        };
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);

        Assert.Equal(58, plan.PlanYears[0].StartingAge);
        Assert.Equal(1000m, plan.PlanYears[0].Accounts[0].BeginningBalance);
        Assert.Equal(1000m, plan.PlanYears[0].EndingBalance);
        Assert.Equal(1000m, plan.PlanYears[1].EndingBalance);
        Assert.Equal(scenario.StartYear, plan.PlanYears[0].StartingYear);
        Assert.Equal(scenario.CurrentAge, plan.PlanYears[0].StartingAge);
        Assert.Equal(scenario.StartYear + 1, plan.PlanYears[1].StartingYear);
        Assert.Equal(scenario.CurrentAge + 1, plan.PlanYears[1].StartingAge);

        Assert.Equal(0m, scenario.Accounts[0].EndingBalance);
    }

    [Theory]
    [InlineData(58, 100, 42)]
    [InlineData(99, 100, 1)]
    [InlineData(100, 100, 0)]
    public void PlanStopsAtLifeExpectancy(int startingAge, int lifeExpectancy, int expectedPeriods)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-startingAge),
            LifeExpectancy = lifeExpectancy,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 1000m }]
        };
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);

        Assert.Equal(expectedPeriods, plan.PlanYears.Count);
        if (expectedPeriods > 0)
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(startingAge, plan.PlanYears[0].StartingAge);
            Assert.Equal(lifeExpectancy, plan.PlanYears[^1].EndingAge);
            Assert.Equal(scenario.EndYear, plan.PlanYears[^1].EndingYear);
        }
    }

    [Theory]
    [InlineData(60, 100)]
    [InlineData(65, 113)]
    [InlineData(66, 0)]
    public void HealthcareStopsAfterInclusiveEndAge(int age, int expected)
    {
        var expense = CreateExpense(60, 65);

        Assert.Equal((decimal)expected, Forecast(expense, age).Amount);
        Assert.Equal(100m, expense.Amount);
    }

    [Fact]
    public void FutureExpenseRetainsItsOriginalCostThroughInactiveYears()
    {
        var expense = CreateExpense(66, 90);
        var inactive = Forecast(expense, 65);

        Assert.Equal(0m, inactive.Amount);
        Assert.Equal(116m, Forecast(expense, 66).Amount);
        Assert.Equal(0m, Forecast(expense, 91).Amount);
    }

    [Fact]
    public void ForecastIsIndependentOfCallOrderAndRoundsOnlyAtTargetAge()
    {
        var expense = CreateExpense(60, 90);
        var later = Forecast(expense, 70);
        var nextYear = Forecast(expense, 61);

        Assert.Equal(103m, nextYear.Amount);
        Assert.Equal(105m, Forecast(expense, 62).Amount);
        Assert.Equal(later.Amount, Forecast(expense, 70).Amount);
        Assert.Equal(100m, expense.Amount);
    }

    [Fact]
    public void InitialForecastPreservesFractionalBaselineBeforeFinalRounding()
    {
        var expense = new Expense(new Expense
        {
            Amount = 100.49m, AgeStart = 60, AgeEnd = 90, AnnualRateOfIncrease = 0.01m
        });

        Assert.Equal(100m, Forecast(expense, 60).Amount);
        Assert.Equal(103m, Forecast(expense, 62).Amount);
    }

    [Fact]
    public void ForecastRejectsAgesBeforeExplicitPlanStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanYearCalculator.ForecastExpense(CreateExpense(60, 90), 60, 59));
    }

    [Fact]
    public void PlanYearsApplyAgeRangesIncludingTheFirstYearAndPreserveScenarioJson()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-65),
            LifeExpectancy = 67,
            AnnualExpenses = 300,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }],
            Expenses =
            [
                new Expense { Name = "Before 66", Amount = 100m, AgeStart = 60, AgeEnd = 65 },
                new Expense { Name = "From 66", Amount = 200m, AgeStart = 66, AgeEnd = 90 }
            ]
        };
        var originalJson = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);

        Assert.True(plan.IsSuccessful);
        Assert.Equal(100m, plan.PlanYears[0].Expenses[0].Amount);
        Assert.Equal(0m, plan.PlanYears[0].Expenses[1].Amount);
        Assert.Equal(0m, plan.PlanYears[1].Expenses[0].Amount);
        Assert.Equal(200m, plan.PlanYears[1].Expenses[1].Amount);
        Assert.Equal(300m, plan.PlanYears[0].TotalExpenses);
        Assert.Equal(400m, plan.PlanYears[1].TotalExpenses);
        Assert.Equal(9700m, plan.PlanYears[0].EndingBalance);
        Assert.Equal(9300m, plan.PlanYears[1].EndingBalance);
        Assert.Equal(originalJson, scenario.Serialize());
        Assert.Equal(originalJson, Scenario.Deserialize(originalJson).Serialize());

        using var json = JsonDocument.Parse(originalJson);
        Assert.Equal(200m, json.RootElement.GetProperty("Expenses")[1].GetProperty("Amount").GetDecimal());
        Assert.False(json.RootElement.GetProperty("Expenses")[1].TryGetProperty("PlanStartAge", out _));
    }

    [Fact]
    public void FutureMedicareDoesNotReduceTheStartingAnnualBudget()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 348942.94m,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 1000000m }],
            Expenses =
            [
                new Expense { Name = "Healthcare", Amount = 39492m, AgeStart = 57, AgeEnd = 64 },
                new Expense { Name = "Medicare", Amount = 8820m, AgeStart = 65, AgeEnd = 100 },
                new Expense { Name = "Charity", Amount = 29952m, AgeStart = 57, AgeEnd = 100 },
                new Expense { Name = "SALT", Amount = 14375m, AgeStart = 57, AgeEnd = 100 }
            ]
        };
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        Assert.Equal(348943m, year.TotalExpenses);
        Assert.Equal(265124m, year.Expenses[^1].Amount);
        Assert.Equal(0m, year.Expenses[1].Amount);
    }

    [Fact]
    public void AllPlansOnlyIncludeNamedExpensesWithinTheirInclusiveAgeRanges()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 62,
            AnnualExpenses = 1000m,
            SecuritiesAnnualRateOfReturn = 0.1m,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 100000m }],
            Expenses =
            [
                new Expense { Name = "Ending now", Amount = 100m, AgeStart = 58, AgeEnd = 58 },
                new Expense { Name = "Future", Amount = 200m, AgeStart = 60, AgeEnd = 60 },
                new Expense { Name = "Expired", Amount = 1000000m, AgeStart = 50, AgeEnd = 57 }
            ]
        };
        var originalJson = scenario.Serialize();
        var calculator = new PlanCalculator();
        foreach (var plan in new[] { calculator.CalculateCurrentTrajectory(scenario),
            calculator.CalculateMaximumSpend(scenario), calculator.CalculatePerpetualSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(plan.AnnualExpenses, plan.PlanYears[0].TotalExpenses);
            Assert.Equal(new[] { 100m, 0m, 0m, 0m },
                plan.PlanYears.Select(year => year.Expenses[0].Amount).ToArray());
            Assert.Equal(new[] { 0m, 0m, 200m, 0m },
                plan.PlanYears.Select(year => year.Expenses[1].Amount).ToArray());
            Assert.All(plan.PlanYears, year => Assert.Equal(0m, year.Expenses[2].Amount));
            var baseline = plan.AnnualExpenses - 100m;
            Assert.Equal(new[] { baseline + 100m, baseline, baseline + 200m, baseline },
                plan.PlanYears.Select(year => year.TotalExpenses).ToArray());
        }
        Assert.Equal(originalJson, scenario.Serialize());
    }

    [Fact]
    public void AnnualForecastsUseOriginalFractionalAmountsForNamedAndUncategorizedExpenses()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-60),
            LifeExpectancy = 64,
            AnnualExpenses = 100.49m,
            AnnualInflationRate = 1m,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }],
            Expenses = [new Expense
            {
                Name = "Future expense", Amount = 100.49m, AgeStart = 62, AgeEnd = 63,
                AnnualRateOfIncrease = 0.01m, UseInflationValue = false
            }]
        };
        var original = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);

        Assert.True(plan.IsSuccessful);
        Assert.Equal(new[] { 0m, 0m, 103m, 104m },
            plan.PlanYears.Select(year => year.Expenses[0].Amount));
        Assert.Equal(new[] { 100m, 101m, 103m, 104m },
            plan.PlanYears.Select(year => year.Expenses[^1].Amount));
        Assert.Equal(original, scenario.Serialize());
    }

    private static Expense Forecast(Expense source, int age)
        => PlanYearCalculator.ForecastExpense(source, 60, age);

    [Fact]
    public void ForecastReturnsSeparateResultsWithoutChangingTheOriginalExpense()
    {
        var expense = CreateExpense(66, 90);
        var inactive = Forecast(expense, 65);
        var active = Forecast(expense, 66);
        var expired = Forecast(expense, 91);

        Assert.NotSame(expense, inactive);
        Assert.NotSame(expense, active);
        Assert.NotSame(active, Forecast(expense, 66));
        Assert.Equal(100m, expense.Amount);
        Assert.Equal(0m, inactive.Amount);
        Assert.Equal(116m, active.Amount);
        Assert.Equal(0m, expired.Amount);
        Assert.Equal(116m, Forecast(expense, 66).Amount);
    }

    private static Expense CreateExpense(int ageStart, int ageEnd) => new()
    {
        Name = "Healthcare",
        Amount = 100m,
        AgeStart = ageStart,
        AgeEnd = ageEnd,
        AnnualRateOfIncrease = 0.025m
    };
}
