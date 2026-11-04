using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class PerpetualAnnualExpenseTests
{
    [Fact]
    public void SearchPreservesInputsAndPreviouslyCalculatedPlans()
    {
        var scenario = CreateScenario(10000m, 0.1m);
        var calculator = new PlanCalculator();
        var plan = calculator.CalculateCurrentTrajectory(scenario);
        var originalBalances = plan.PlanYears.Select(year => year.EndingBalance).ToArray();
        var originalJson = scenario.Serialize();

        Assert.Equal(1000m, calculator.CalculatePerpetualSpend(scenario).AnnualExpenses);
        Assert.Equal(1000m, calculator.CalculatePerpetualSpend(scenario).AnnualExpenses);
        Assert.Equal(originalBalances, plan.PlanYears.Select(year => year.EndingBalance).ToArray());
        Assert.Equal(originalJson, scenario.Serialize());
    }

    [Fact]
    public void EmptyAccountsDoNotAffectFinalYearGrowthTarget()
    {
        var scenario = CreateScenario(1010123m, 0.07m);
        scenario.AnnualInflationRate = 6m;
        scenario.BondsAnnualRateOfReturn = 0.0453m;
        scenario.Accounts[0].Type = AccountType.Brokerage;
        var calculator = new PlanCalculator();
        var expected = calculator.CalculatePerpetualSpend(scenario);
        scenario.Accounts.Insert(0, new Account
        {
            Type = AccountType.Inherited, Holdings = AccountHoldings.Bonds,
            BeginningBalance = 0m, WithdrawalPriority = 0
        });
        var original = scenario.Serialize();

        var actual = calculator.CalculatePerpetualSpend(scenario);

        Assert.True(actual.IsSuccessful);
        Assert.Equal(expected.AnnualExpenses, actual.AnnualExpenses);
        Assert.Equal(original, scenario.Serialize());
        Assert.All(actual.PlanYears, year => Assert.Equal(0m, year.Accounts[0].EndingBalance));
    }

    [Fact]
    public void ReturnsFailureWhenEvenZeroSpendingCannotMatchInflation()
    {
        var scenario = CreateScenario(10000m, 0.03m);
        scenario.AnnualInflationRate = 4m;
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Empty(plan.PlanYears);
        Assert.Contains("at least 4%", plan.FailureReason);
    }

    [Fact]
    public void OverflowingForecastDoesNotMeetGrowthTarget()
    {
        var scenario = CreateScenario(1000000m, 0.1m);
        scenario.LifeExpectancy = 10001;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        scenario.Expenses = [new Expense
        {
            Name = "Long-lived expense", Amount = 1000m, AgeStart = 58, AgeEnd = 10000
        }];
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Empty(plan.PlanYears);
    }

    [Theory]
    [InlineData(59, 0, 100000)]
    [InlineData(59, 2, 80000)]
    [InlineData(59, 4, 60000)]
    [InlineData(59, 6, 40000)]
    [InlineData(60, 4, 60000)]
    public void FindsClosestGrowthAtOrAboveInflationInFinalYear(int lifeExpectancy, int inflation, int expectedBudget)
    {
        var scenario = CreateScenario(500000m, 0.1m);
        scenario.LifeExpectancy = lifeExpectancy;
        scenario.AnnualInflationRate = inflation;
        scenario.Accounts.Add(new Account { Type = AccountType.RetirementRoth, BeginningBalance = 500000m });
        var calculator = new PlanCalculator();
        var plan = calculator.CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Equal(expectedBudget, plan.AnnualExpenses);
        var previous = plan.PlanYears.Count == 1 ? 1000000m : plan.PlanYears[^2].EndingBalance;
        Assert.True(plan.PlanYears[^1].EndingBalance >= previous * (1m + inflation / 100m));
        if (lifeExpectancy == 59)
            Assert.Equal(1000000m * (1m + inflation / 100m), plan.PlanYears[^1].EndingBalance);
        else
            Assert.True(plan.PlanYears[0].EndingBalance >= 1000000m * (1m + inflation / 100m));
        scenario.FirstYearAnnualExpenses = plan.AnnualExpenses + 1000m;
        var higher = calculator.CalculateCurrentTrajectory(scenario);
        var higherPrevious = higher.PlanYears.Count == 1 ? 1000000m : higher.PlanYears[^2].EndingBalance;
        Assert.True(higher.PlanYears[^1].EndingBalance < higherPrevious * (1m + inflation / 100m));
    }

    [Fact]
    public void ZeroBalancesDoNotCountAsGrowth()
    {
        var scenario = CreateScenario(0m, 0.1m);
        Assert.False(new PlanCalculator().CalculatePerpetualSpend(scenario).IsSuccessful);
    }

    private static Scenario CreateScenario(decimal balance, decimal returnRate)
    {
        return new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            FirstYearAnnualExpenses = 1000m,
            SecuritiesAnnualRateOfReturn = returnRate,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = balance }]
        };
    }
}
