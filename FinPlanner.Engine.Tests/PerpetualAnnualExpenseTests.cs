using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class PerpetualAnnualExpenseTests
{
    [Theory]
    [InlineData(59)]
    [InlineData(100)]
    [InlineData(58)]
    public void ConstantSpendingCanBeFundedForeverWithoutDependingOnLifeExpectancy(int lifeExpectancy)
    {
        var scenario = CreateScenario(10000m, 0.1m);
        scenario.LifeExpectancy = lifeExpectancy;
        Assert.Equal(1000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void PerpetualSpendingContinuesGrowingWithInflation()
    {
        var scenario = CreateScenario(110000m, 0.1m);
        scenario.AnnualInflationRate = 2m;

        Assert.Equal(8000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.02, 2)]
    [InlineData(0.01, 2)]
    public void SpendingCannotGrowAsFastAsOrFasterThanEveryAccount(double rate, int inflation)
    {
        var scenario = CreateScenario(100000m, (decimal)rate);
        scenario.AnnualInflationRate = inflation;

        Assert.Equal(0m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void SlowDepletionIsRejectedWithoutWaitingThousandsOfYears()
    {
        var scenario = CreateScenario(10000000m, 0.0001m);

        Assert.Equal(1000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void AccountsWithTheSameReturnCanCollectivelyFundPerpetualSpending()
    {
        var scenario = CreateScenario(110000m, 0.1m);
        scenario.AnnualInflationRate = 2m;
        scenario.Accounts =
        [
            new Account { BeginningBalance = 55000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 55000m, WithdrawalPriority = 2 }
        ];

        Assert.Equal(8000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void NamedExpensesStopAtTheirConfiguredAgeEnd()
    {
        var scenario = CreateScenario(100000m, 0.1m);
        scenario.Expenses = [new Expense
        {
            Name = "Temporary expense", Amount = 5000m, AgeStart = 58, AgeEnd = 59
        }];

        Assert.Equal(14000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Theory]
    [InlineData(59, 9000)]
    [InlineData(60, 8000)]
    [InlineData(100, 8000)]
    public void LifetimeExpensesContinueAtTheirOwnGrowthRate(int ageEnd, int expectedExpenses)
    {
        var scenario = CreateScenario(110000m, 0.1m);
        scenario.AnnualInflationRate = 2m;
        scenario.Expenses = [new Expense
        {
            Name = "Ongoing expense", Amount = 1000m, AgeStart = 58, AgeEnd = ageEnd,
            AnnualRateOfIncrease = 0.04m, UseInflationValue = false
        }];

        Assert.Equal((decimal)expectedExpenses,
            new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
        Assert.Equal(ageEnd, scenario.Expenses[0].AgeEnd);
        Assert.Equal(0.04m, scenario.Expenses[0].AnnualRateOfIncrease);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.11)]
    public void AnOngoingExpenseCannotGrowAsFastAsOrFasterThanAccountReturns(double growthRate)
    {
        var scenario = CreateScenario(100000m, 0.1m);
        scenario.Expenses = [new Expense
        {
            Name = "Ongoing expense", Amount = 1000m, AgeStart = 58, AgeEnd = 60,
            AnnualRateOfIncrease = (decimal)growthRate
        }];

        Assert.Equal(0m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void OngoingExpensesRetainTheirFutureStartAge()
    {
        var scenario = CreateScenario(110000m, 0.1m);
        scenario.LifeExpectancy = 70;
        scenario.AnnualInflationRate = 2m;
        scenario.Expenses = [new Expense
        {
            Name = "Future ongoing expense", Amount = 5000m, AgeStart = 65, AgeEnd = 70,
            AnnualRateOfIncrease = 0.04m
        }];

        Assert.Equal(9000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void CalculatedPlansPreserveLifetimeAndExpenseLimitsAfterScenarioEdits()
    {
        var scenario = CreateScenario(110000m, 0.1m);
        scenario.AnnualInflationRate = 2m;
        scenario.Expenses = [new Expense
        {
            Name = "Ongoing expense", Amount = 1000m, AgeStart = 58, AgeEnd = 60,
            AnnualRateOfIncrease = 0.04m
        }];
        var calculator = new PlanCalculator();
        var plan = calculator.CalculateCurrentTrajectory(scenario);
        var perpetualPlan = calculator.CalculatePerpetualSpend(scenario);
        scenario.LifeExpectancy = 100;

        Assert.Equal(8000m, perpetualPlan.AnnualExpenses);
        Assert.Equal(60, perpetualPlan.PlanYears[^1].EndingAge);
        Assert.Equal(2, plan.PlanYears.Count);
        Assert.Equal(60, plan.PlanYears[^1].EndingAge);
        Assert.Equal(130960m, plan.PlanYears[^1].EndingBalance);
        Assert.All(plan.PlanYears, year => Assert.Equal(60, year.Expenses[0].AgeEnd));
        Assert.Equal(60, scenario.Expenses[0].AgeEnd);
    }

    [Fact]
    public void OngoingConstantExpensesCanBeFundedWithNoUncategorizedSpending()
    {
        var scenario = CreateScenario(100000m, 0.1m);
        scenario.Expenses = [new Expense
        {
            Name = "Ongoing expense", Amount = 10000m, AgeStart = 58, AgeEnd = 60
        }];

        Assert.Equal(10000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void SearchHonorsDifferentReturnsAndWithdrawalPriorities()
    {
        var scenario = CreateScenario(50000m, 0.1m);
        scenario.BondsAnnualRateOfReturn = 0.04m;
        scenario.Accounts =
        [
            new Account { BeginningBalance = 50000m, Holdings = AccountHoldings.Bonds, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 50000m, Holdings = AccountHoldings.Equities, WithdrawalPriority = 2 }
        ];

        Assert.Equal(9000m, new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

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
    public void ALongSimulationIsNotAcceptedAsProof()
    {
        var scenario = CreateScenario(1000000m, 0.1m);
        scenario.LifeExpectancy = 10001;
        scenario.Expenses = [new Expense
        {
            Name = "Long-lived expense", Amount = 1000m, AgeStart = 58, AgeEnd = 10000
        }];

        Assert.Throws<InvalidOperationException>(() =>
            new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Fact]
    public void NegativeRatesAreExplicitlyUnsupported()
    {
        var scenario = CreateScenario(10000m, -0.01m);

        Assert.Throws<NotSupportedException>(() =>
            new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    private static Scenario CreateScenario(decimal balance, decimal returnRate)
    {
        return new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 1000m,
            SecuritiesAnnualRateOfReturn = returnRate,
            Accounts = [new Account { BeginningBalance = balance }]
        };
    }
}
