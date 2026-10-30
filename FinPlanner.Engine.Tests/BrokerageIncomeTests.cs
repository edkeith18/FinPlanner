using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class BrokerageIncomeTests
{
    [Theory]
    [InlineData(AccountType.Inherited, 58, 28300, 1000)]
    [InlineData(AccountType.Retirement, 75, 24600, 1000)]
    public void RmdTransfersExistingFundsWithoutCreatingIncome(
        AccountType type, int age, int balance, int expectedRmd)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-age),
            LifeExpectancy = age + 1,
            SecuritiesAnnualRateOfReturn = 0.1m,
            Accounts =
            [
                new Account { Type = type, BeginningBalance = balance },
                new Account { Type = AccountType.Brokerage }
            ]
        };
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        var retirement = year.AccountEarnings[0];
        Assert.Equal((decimal)expectedRmd, year.MandatoryFrom(0));
        Assert.Equal(0m, retirement.EarnedAmount);
        Assert.Equal(balance * 1.1m - expectedRmd, year.Accounts[0].EndingBalance);
        Assert.Equal((decimal)expectedRmd, year.Accounts[1].EndingBalance);
        Assert.Equal(balance * 1.1m, year.EndingBalance);
    }

    [Fact]
    public void PerpetualBrokerageSpendingIncludesAppreciationAndEarnedIncome()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            SecuritiesAnnualRateOfReturn = 0m,
            Accounts = [new Account { BeginningBalance = 100000m }]
        };
        // Even with zero appreciation, 1.16% in earned income can fund $1,000.
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Equal(1000m, plan.AnnualExpenses);
        Assert.Equal(100160m, plan.PlanYears[0].EndingBalance);
    }

    [Fact]
    public void BrokerageBondsUseJsonBondRateForInterestAndFundExpenses()
    {
        var scenario = Scenario.Deserialize("""
            {
              "DateOfBirth": "10/21/1968",
              "LifeExpectancy": 100,
              "AnnualExpenses": 5000,
              "BondsAnnualRateOfReturn": 4.48,
              "SecuritiesAnnualRateOfReturn": 7,
              "Accounts": [{ "Type": "Brokerage", "Holdings": "Bonds", "Balance": 100000 }]
            }
            """);
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        var income = year.AccountEarnings[0];
        Assert.Equal(4480m, income.Interest);
        Assert.Equal(0m, income.DivQ);
        Assert.Equal(0m, income.DivUQ);
        Assert.Equal(4480m, income.Amount);
        Assert.Equal(520m, year.VoluntaryFrom(0));
        Assert.Equal(103960m, year.EndingBalance);

        var next = PlanYearCalculator.ForecastFrom(year, scenario, scenario.AnnualExpenses);
        PlanYearCalculator.CalculateEarnings(next);
        Assert.Equal(0.0448m, next.Accounts[0].AppreciationRate);
        Assert.Equal(4657m, next.AccountEarnings[0].Interest);
    }

    [Fact]
    public void BondInterestAddsToAppreciationInAllPlanningModes()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            BondsAnnualRateOfReturn = 0.05m,
            Accounts = [new Account { Type = AccountType.Brokerage,
                Holdings = AccountHoldings.Bonds, BeginningBalance = 100000m }]
        };
        var calculator = new PlanCalculator();
        var current = calculator.CalculateCurrentTrajectory(scenario);
        Assert.Equal(110000m, current.PlanYears[0].EndingBalance);
        foreach (var plan in new[] { current, calculator.CalculateMaximumSpend(scenario),
            calculator.CalculatePerpetualSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(5000m, plan.PlanYears[0].AccountEarnings[0].Interest);
            Assert.Equal(5000m, plan.PlanYears[0].AccountEarnings[0].Amount);
        }
    }

    [Theory]
    [InlineData(AccountHoldings.Equities, 1160)]
    [InlineData(AccountHoldings.Bonds, 5000)]
    public void BrokerageIncomeUsesTheScheduleForItsHoldings(AccountHoldings holdings, int expected)
    {
        var income = PlanYearCalculator.CalculateAccountEarnings(new Account
        {
            Type = AccountType.Brokerage, Holdings = holdings, BeginningBalance = 100000m,
            AppreciationRate = 0.05m
        });
        Assert.Equal((decimal)expected, income.Amount);
        Assert.Equal((decimal)expected, income.Interest + income.DivQ + income.DivUQ);
    }

    [Fact]
    public void BrokerageCalculatesThreeSeparatelyRoundedMandatoryComponents()
    {
        var income = PlanYearCalculator.CalculateAccountEarnings(new Account
        {
            Type = AccountType.Brokerage, BeginningBalance = 100000m
        });

        Assert.Equal(170m, income.Interest);
        Assert.Equal(940m, income.DivQ);
        Assert.Equal(50m, income.DivUQ);
        Assert.Equal(0m, PlanYearCalculator.CreateRmdTransfer(income.Account, 58).MandatoryAmount);
        Assert.Equal(1160m, income.Amount);
        Assert.Equal(1160m, income.Amount);
    }

    [Theory]
    [InlineData(1000, 2, 9, 1)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(-1000, 0, 0, 0)]
    public void ComponentsUseWholeDollarRoundingAndNonnegativeBalances(
        int balance, int interest, int divQ, int divUQ)
    {
        var income = PlanYearCalculator.CalculateAccountEarnings(new Account { BeginningBalance = balance });
        Assert.Equal((decimal)interest, income.Interest);
        Assert.Equal((decimal)divQ, income.DivQ);
        Assert.Equal((decimal)divUQ, income.DivUQ);
        Assert.Equal((decimal)(interest + divQ + divUQ), income.Amount);
    }

    [Theory]
    [InlineData(AccountType.Retirement)]
    [InlineData(AccountType.Inherited)]
    [InlineData(AccountType.RetirementRoth)]
    [InlineData(AccountType.Hsa)]
    public void OtherAccountTypesDoNotReceiveBrokerageIncome(AccountType type)
    {
        var income = PlanYearCalculator.CalculateAccountEarnings(new Account { Type = type, BeginningBalance = 100000m });
        Assert.Equal(0m, income.Interest + income.DivQ + income.DivUQ);
        Assert.Equal(0m, income.Amount);
    }

    [Fact]
    public void BrokerageIncomeFundsExpensesBeforeVoluntaryWithdrawalsAndRecalculatesNextPeriod()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 2000m,
            SecuritiesAnnualRateOfReturn = 0.1m,
            Accounts = [new Account { BeginningBalance = 100000m }]
        };
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        var first = plan.PlanYears[0];
        Assert.Equal(1160m, first.AccountEarnings[0].Amount);
        Assert.Equal(840m, first.VoluntaryFrom(0));
        Assert.Equal(109160m, first.EndingBalance);
        var next = plan.PlanYears[1].AccountEarnings[0];
        Assert.Equal(186m, next.Interest);
        Assert.Equal(1026m, next.DivQ);
        Assert.Equal(55m, next.DivUQ);
        Assert.Equal(1267m, next.Amount);
    }

    [Fact]
    public void UnspentBrokerageIncomeAddsToAppreciation()
    {
        var account = new Account { BeginningBalance = 100000m };
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 59,
            SecuritiesAnnualRateOfReturn = 0.07m,
            Accounts = [account]
        };
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        Assert.Equal(1160m, year.AccountEarnings[0].Amount);
        Assert.Equal(0m, year.VoluntaryFrom(0));
        Assert.Equal(108160m, year.EndingBalance);
        Assert.Equal(100000m, account.BeginningBalance);
    }

    [Fact]
    public void EarnedIncomeIsNewValueEvenWhenTheExistingBalanceIsSmall()
    {
        var year = PlanYearCalculator.FromScenario(new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            Accounts = [new Account { BeginningBalance = 100000m }]
        }, 0m);
        year.Accounts[0].EndingBalance = 200m;
        PlanYearCalculator.CalculateEarnings(year);
        PlanYearCalculator.ApplyRmdsAndScheduledTransfers(year);
        PlanYearCalculator.ApplyExpenses(year);
        var income = year.AccountEarnings[0];
        Assert.Equal(170m, income.Interest);
        Assert.Equal(940m, income.DivQ);
        Assert.Equal(50m, income.DivUQ);
        Assert.Equal(1160m, income.Amount);
        Assert.Equal(1360m, year.EndingBalance);
        Assert.Equal(income.Interest + income.DivQ + income.DivUQ,
            income.Amount);
    }
}
