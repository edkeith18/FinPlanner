using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class PlanCalculatorTests
{
    [Fact]
    public void AllCalculationsReturnCompletedNamedPlansWithoutChangingTheScenario()
    {
        var scenario = CreateScenario();
        var originalJson = scenario.Serialize();
        var calculator = new PlanCalculator();

        var current = calculator.CalculateCurrentTrajectory(scenario);
        var maximum = calculator.CalculateMaximumSpend(scenario);
        var perpetual = calculator.CalculatePerpetualSpend(scenario);

        Assert.Equal("Financial Plan", current.Name);
        Assert.Equal("Maximum Spend Plan", maximum.Name);
        Assert.Equal("Perpetual Spend Plan", perpetual.Name);
        Assert.Equal(1000m, current.AnnualExpenses);
        Assert.Equal(5000m, maximum.AnnualExpenses);
        Assert.Equal(1000m, perpetual.AnnualExpenses);
        Assert.Equal(10000m, current.PlanYears[^1].EndingBalance);
        Assert.Equal(1600m, maximum.PlanYears[^1].EndingBalance);
        Assert.Equal(10000m, perpetual.PlanYears[^1].EndingBalance);
        foreach (var plan in new[] { current, maximum, perpetual })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Null(plan.FailureReason);
            Assert.Equal(2, plan.PlanYears.Count);
            Assert.Equal(58, plan.PlanYears[0].StartingAge);
            Assert.Equal(60, plan.PlanYears[^1].EndingAge);
            Assert.Equal(scenario.StartYear, plan.BeginningYear);
            Assert.Equal(scenario.EndYear, plan.EndingYear);
        }
        Assert.NotSame(current.PlanYears[0].Accounts[0], maximum.PlanYears[0].Accounts[0]);
        Assert.NotSame(current.PlanYears[0].Accounts[0], perpetual.PlanYears[0].Accounts[0]);
        Assert.Equal(originalJson, scenario.Serialize());
    }

    [Fact]
    public void ProvenFailuresReturnNamedPlansWithReasons()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 100m;
        scenario.SecuritiesAnnualRateOfReturn = 0m;
        scenario.Expenses = [new Expense
        {
            Name = "Required expense", Amount = 200m, AgeStart = 58, AgeEnd = 60
        }];
        var calculator = new PlanCalculator();

        foreach (var plan in new[]
        {
            calculator.CalculateMaximumSpend(scenario),
            calculator.CalculatePerpetualSpend(scenario)
        })
        {
            Assert.False(plan.IsSuccessful);
            Assert.Equal("No sustainable spending plan was found.", plan.FailureReason);
            Assert.Empty(plan.PlanYears);
            Assert.Equal(0m, plan.AnnualExpenses);
        }
    }

    [Fact]
    public void SustainableZeroSpendingIsDistinctFromFailure()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 1000m;
        scenario.SecuritiesAnnualRateOfReturn = 0m;
        var calculator = new PlanCalculator();

        foreach (var plan in new[]
        {
            calculator.CalculateMaximumSpend(scenario),
            calculator.CalculatePerpetualSpend(scenario)
        })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(0m, plan.AnnualExpenses);
            Assert.Equal(1000m, plan.PlanYears[^1].EndingBalance);
        }
    }

    [Fact]
    public void CurrentTrajectoryKeepsResultsThroughTheYearItFails()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 1000m;
        scenario.SecuritiesAnnualRateOfReturn = 0m;
        scenario.AnnualExpenses = 600m;

        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);

        Assert.False(plan.IsSuccessful);
        Assert.Equal($"Plan fails at year {scenario.StartYear + 1}.", plan.FailureReason);
        Assert.Equal(2, plan.PlanYears.Count);
        Assert.Equal(400m, plan.PlanYears[0].EndingBalance);
        Assert.Equal(0m, plan.PlanYears[1].EndingBalance);
    }

    [Fact]
    public void ReusingCalculatorUsesTheInputsSuppliedForEachCall()
    {
        var scenario = CreateScenario();
        var calculator = new PlanCalculator();
        var original = calculator.CalculateCurrentTrajectory(scenario);

        scenario.AnnualExpenses = 2000m;
        var updated = calculator.CalculateCurrentTrajectory(scenario);

        Assert.Equal(1000m, original.AnnualExpenses);
        Assert.Equal(2000m, updated.AnnualExpenses);
        Assert.Equal(10000m, original.PlanYears[0].EndingBalance);
        Assert.Equal(9000m, updated.PlanYears[0].EndingBalance);
    }

    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
        LifeExpectancy = 60,
        AnnualExpenses = 1000m,
        SecuritiesAnnualRateOfReturn = 0.1m,
        Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }]
    };
}
