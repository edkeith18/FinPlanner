using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class RmdCalculatorTests
{
    [Theory]
    [InlineData(AccountType.Retirement, 74, 0)]
    [InlineData(AccountType.Retirement, 75, 1000)]
    [InlineData(AccountType.Retirement, 76, 1038)]
    [InlineData(AccountType.RetirementRoth, 75, 0)]
    [InlineData(AccountType.Hsa, 75, 0)]
    [InlineData(AccountType.Brokerage, 75, 0)]
    public void RetirementUsesStartingAgeAndV2Divisors(AccountType type, int age, int expected)
        => Assert.Equal((decimal)expected, RmdCalculator.Calculate(type, 24600m, age));

    [Theory]
    [InlineData(51, 0)]
    [InlineData(52, 1000)]
    [InlineData(58, 1212)]
    [InlineData(85, 26385)]
    [InlineData(86, 34300)]
    [InlineData(100, 34300)]
    [InlineData(300, 34300)]
    public void InheritedUsesPersonalScheduleWithOneAsMinimumDivisor(int age, int expected)
        => Assert.Equal((decimal)expected, RmdCalculator.Calculate(AccountType.Inherited, 34300m, age));

    [Theory]
    [InlineData(100, 6.4)]
    [InlineData(101, 6.0)]
    [InlineData(119, 2.3)]
    [InlineData(120, 2.0)]
    [InlineData(300, 2.0)]
    public void RetirementSupportsForecastsBeyondAge100(int age, double divisor)
        => Assert.Equal((decimal)divisor, RmdCalculator.GetDivisor(AccountType.Retirement, age));

    [Theory]
    [InlineData(AccountType.Retirement)]
    [InlineData(AccountType.Inherited)]
    public void EmptyOrNegativeBalancesHaveNoRmd(AccountType type)
    {
        Assert.Equal(0m, RmdCalculator.Calculate(type, 0m, 80));
        Assert.Equal(0m, RmdCalculator.Calculate(type, -100m, 80));
    }

    [Theory]
    [InlineData(AccountType.Retirement, 74, 24600)]
    [InlineData(AccountType.Inherited, 51, 34300)]
    public void ForecastStartsRmdAtEligibilityAndUsesOpeningBalance(
        AccountType type, int age, int balance)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-age),
            LifeExpectancy = age + 2,
            SecuritiesAnnualRateOfReturn = 0.1m,
            Accounts =
            [
                new Account { Type = type, BeginningBalance = balance },
                new Account { Type = AccountType.Brokerage }
            ]
        };
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Equal(0m, plan.PlanYears[0].MandatoryFrom(0));
        Assert.Equal(1100m, plan.PlanYears[1].MandatoryFrom(0));
        Assert.Equal(1100m, plan.PlanYears[1].Accounts[1].EndingBalance);
    }

    [Fact]
    public void PerpetualProofDoesNotIgnoreFutureRetirementSurplus()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-74),
            LifeExpectancy = 76,
            SecuritiesAnnualRateOfReturn = 0.1m,
            Accounts = [new Account { Type = AccountType.Retirement, BeginningBalance = 100000m }]
        };
        // Zero spending looks sustainable before 75, but future mandatory income
        // needs a Brokerage destination, just as it does in a finite plan.
        Assert.Throws<InvalidOperationException>(() =>
            new PlanCalculator().CalculatePerpetualSpend(scenario));
    }
}
