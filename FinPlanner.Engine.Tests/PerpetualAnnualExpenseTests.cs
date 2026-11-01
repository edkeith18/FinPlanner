using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class PerpetualAnnualExpenseTests
{
    [Theory]
    [InlineData(0.02, 0)]
    [InlineData(0.1, 1000)]
    public void MandatorySurplusTransfersAffectPerpetualReturns(double brokerageReturn, int expected)
    {
        var scenario = CreateScenario(11000m, 0.1m);
        scenario.BondsAnnualRateOfReturn = (decimal)brokerageReturn;
        scenario.Accounts =
        [
            new Account { Type = AccountType.Inherited, BeginningBalance = 11000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Brokerage, Holdings = AccountHoldings.Bonds, WithdrawalPriority = 2 }
        ];

        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);

        Assert.True(plan.IsSuccessful);
        Assert.Equal((decimal)expected, plan.AnnualExpenses);
        // At 58 the personal RMD is below $1,000, so that spending case
        // initially consumes it all. The zero-spending case transfers surplus.
        Assert.Equal(expected == 0, plan.PlanYears[0].Accounts[1].EndingBalance > 0m);
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
    public void NegativeRatesAreExplicitlyUnsupported()
    {
        var scenario = CreateScenario(10000m, -0.01m);

        Assert.Throws<NotSupportedException>(() =>
            new PlanCalculator().CalculatePerpetualSpend(scenario).AnnualExpenses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAccountsThatCannotReceiveSurplusDoNotPreventPerpetualProof(bool withIncome)
    {
        var scenario = CreateScenario(1010123m, 0.07m);
        scenario.AnnualInflationRate = 6m;
        scenario.BondsAnnualRateOfReturn = 0.0453m;
        scenario.Accounts[0].Type = AccountType.Brokerage;
        if (withIncome)
            scenario.Income.Add(new Income { InitialAmount = 1000m, AgeStart = 58 });
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
    public void MissingCertificateDoesNotClaimThatNoSustainableBudgetExists()
    {
        var scenario = CreateScenario(10000m, 0.1m);
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Empty(plan.PlanYears);
        Assert.Contains("does not prove", plan.FailureReason);
    }

    [Fact]
    public void CertifiedBudgetIncludesTaxesAndPreservesTheReportingHorizon()
    {
        var scenario = CreateScenario(1000000m, 0.07m);
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.AnnualExpenses > 0m);
        Assert.Equal(2, plan.PlanYears.Count);
        Assert.Contains("exact maximum is not proven", plan.CalculationNote);
        Assert.Null(plan.PlanYears[0].Taxes);
        Assert.All(plan.PlanYears.Skip(1), year => Assert.NotNull(year.Taxes));
    }

    [Fact]
    public void LongObligationsBeyondTheProofHorizonAreNotCertified()
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
