using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ScenarioSnapshotTests
{
    [Fact]
    public void CalculatedPlansRetainSnapshotsOfTheirInputs()
    {
        var scenario = new Scenario
        {
            StartYear = 2026,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 1000m,
            AnnualInflationRate = 4m,
            SecuritiesAnnualRateOfReturn = 0.07m,
            BondsAnnualRateOfReturn = 0.04m,
            Accounts = [new Account { Name = "Savings", BeginningBalance = 100000m }],
            Expenses = [new Expense
            {
                Name = "Healthcare", Amount = 100m, AgeStart = 58, AgeEnd = 100,
                AnnualRateOfIncrease = 0.01m
            }]
        };
        var snapshot = new Scenario(scenario);
        var calculator = new PlanCalculator();
        var plan = calculator.CalculateCurrentTrajectory(scenario);
        var maximumPlan = calculator.CalculateMaximumSpend(scenario);
        var perpetualPlan = calculator.CalculatePerpetualSpend(scenario);
        Assert.True(perpetualPlan.IsSuccessful);
        scenario.Accounts[0].BeginningBalance = 1000000m;
        scenario.Accounts[0].Holdings = AccountHoldings.Bonds;
        scenario.Expenses[0].Amount = 50000m;
        scenario.Expenses[0].AnnualRateOfIncrease = 1m;
        scenario.Accounts.Clear();
        scenario.Expenses.Clear();
        scenario.StartYear = 2040;
        scenario.DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-70);
        scenario.LifeExpectancy = 90;
        scenario.AnnualExpenses = 100000m;
        scenario.AnnualInflationRate = 50m;
        scenario.SecuritiesAnnualRateOfReturn = 1m;
        scenario.BondsAnnualRateOfReturn = 1m;

        var expectedPlan = calculator.CalculateCurrentTrajectory(snapshot);
        var expectedMaximumPlan = calculator.CalculateMaximumSpend(snapshot);
        var expectedPerpetualPlan = calculator.CalculatePerpetualSpend(snapshot);
        Assert.Equal(JsonSerializer.Serialize(expectedPlan.PlanYears), JsonSerializer.Serialize(plan.PlanYears));
        Assert.Equal(expectedMaximumPlan.AnnualExpenses, maximumPlan.AnnualExpenses);
        Assert.Equal(JsonSerializer.Serialize(expectedMaximumPlan.PlanYears), JsonSerializer.Serialize(maximumPlan.PlanYears));
        Assert.Equal(expectedPerpetualPlan.AnnualExpenses, perpetualPlan.AnnualExpenses);
        Assert.Equal(JsonSerializer.Serialize(expectedPerpetualPlan.PlanYears), JsonSerializer.Serialize(perpetualPlan.PlanYears));
    }

    [Fact]
    public void SnapshotCopiesCollectionItemsAndDoesNotCopyEventSubscriptions()
    {
        var scenario = new Scenario
        {
            StartYear = 2026,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 100,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 1000.49m, EndingBalance = 2000.51m }],
            Expenses = [new Expense { Name = "Expense", Amount = 100m }],
            Income = [new Income { Name = "Incomes", Amount = 200m }],
            Transfers = [new Transfer { Name = "Transfer", Amount = 300m }]
        };
        var notifications = 0;
        scenario.Changed += () => notifications++;

        var snapshot = new Scenario(scenario);
        Assert.NotSame(scenario.Accounts[0], snapshot.Accounts[0]);
        Assert.NotSame(scenario.Expenses[0], snapshot.Expenses[0]);
        Assert.NotSame(scenario.Income[0], snapshot.Income[0]);
        Assert.NotSame(scenario.Transfers[0], snapshot.Transfers[0]);
        Assert.Equal(1000.49m, snapshot.Accounts[0].BeginningBalance);
        Assert.Equal(2000.51m, snapshot.Accounts[0].EndingBalance);
        Assert.Equal(scenario.StartYear, snapshot.StartYear);
        Assert.Equal(scenario.CurrentAge, snapshot.CurrentAge);

        scenario.Income[0].Amount = 999m;
        scenario.Transfers[0].Amount = 999m;
        Assert.Equal(200m, snapshot.Income[0].Amount);
        Assert.Equal(300m, snapshot.Transfers[0].Amount);
        snapshot.NotifyStateChanged();
        Assert.Equal(0, notifications);
    }
}
