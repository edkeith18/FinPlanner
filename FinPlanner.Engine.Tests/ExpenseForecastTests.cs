using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ExpenseForecastTests
{
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
        Assert.Equal(116m, Forecast(inactive, 66).Amount);
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
        Assert.Equal(105m, Forecast(nextYear, 62).Amount);
        Assert.Equal(105m, Forecast(expense, 62).Amount);
        Assert.Equal(Forecast(expense, 70).Amount, Forecast(later, 70).Amount);
        Assert.Equal(105m, Forecast(later, 62).Amount);
    }

    [Fact]
    public void InitialForecastPreservesFractionalBaselineBeforeFinalRounding()
    {
        var expense = new Expense(new Expense
        {
            Amount = 100.49m, AgeStart = 60, AgeEnd = 90, AnnualRateOfIncrease = 0.01m
        }) { PlanStartAge = 60 };

        Assert.Equal(100m, Forecast(expense, 60).Amount);
        Assert.Equal(103m, Forecast(expense, 62).Amount);
    }

    [Fact]
    public void ForecastRequiresPlanStartAndRejectsEarlierAges()
    {
        Assert.Throws<InvalidOperationException>(() => new Expense().ForecastFromAge(60));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateExpense(60, 90).ForecastFromAge(59));
    }

    [Fact]
    public void PlanYearsApplyAgeRangesIncludingTheFirstYearAndPreserveScenarioJson()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-65),
            LifeExpectancy = 67,
            AnnualExpenses = 300,
            Accounts = [new Account { BeginningBalance = 10000m }],
            Expenses =
            [
                new Expense { Name = "Before 66", Amount = 100m, AgeStart = 60, AgeEnd = 65 },
                new Expense { Name = "From 66", Amount = 200m, AgeStart = 66, AgeEnd = 90 }
            ]
        };
        var originalJson = scenario.Serialize();
        var plan = new Plan(scenario);
        plan.Build();

        Assert.True(plan.IsSuccessful);
        Assert.Equal(100m, plan.PlanYears[0].Expenses[0].Amount);
        Assert.Equal(0m, plan.PlanYears[0].Expenses[1].Amount);
        Assert.Equal(0m, plan.PlanYears[1].Expenses[0].Amount);
        Assert.Equal(200m, plan.PlanYears[1].Expenses[1].Amount);
        Assert.Equal(100m, plan.PlanYears[0].TotalExpenses);
        Assert.Equal(200m, plan.PlanYears[1].TotalExpenses);
        Assert.Equal(9900m, plan.PlanYears[0].EndingBalance);
        Assert.Equal(9700m, plan.PlanYears[1].EndingBalance);
        Assert.Equal(originalJson, scenario.Serialize());
        Assert.Equal(originalJson, Scenario.Deserialize(originalJson).Serialize());

        using var json = JsonDocument.Parse(originalJson);
        Assert.Equal(200m, json.RootElement.GetProperty("Expenses")[1].GetProperty("Amount").GetDecimal());
        Assert.False(json.RootElement.GetProperty("Expenses")[1].TryGetProperty("PlanStartAge", out _));
    }

    private static Expense Forecast(Expense source, int age)
    {
        var forecast = new Expense(source);
        forecast.ForecastFromAge(age);
        return forecast;
    }

    [Fact]
    public void ForecastUpdatesTheSameExpenseWithoutLosingItsBaseline()
    {
        var expense = CreateExpense(66, 90);
        expense.ForecastFromAge(65);
        Assert.Equal(0m, expense.Amount);
        expense.ForecastFromAge(66);
        Assert.Equal(116m, expense.Amount);
        expense.ForecastFromAge(66);
        Assert.Equal(116m, expense.Amount);
        expense.ForecastFromAge(91);
        Assert.Equal(0m, expense.Amount);
        expense.ForecastFromAge(66);
        Assert.Equal(116m, expense.Amount);
    }
    private static Expense CreateExpense(int ageStart, int ageEnd) => new(new Expense
    {
        Name = "Healthcare",
        Amount = 100m,
        AgeStart = ageStart,
        AgeEnd = ageEnd,
        AnnualRateOfIncrease = 0.025m
    }) { PlanStartAge = 60 };
}
