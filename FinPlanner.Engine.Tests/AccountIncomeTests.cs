using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class AccountIncomeTests
{
    [Fact]
    public void AllCalculatorsUseAccountIncomeWithoutChangingScenarioInputs()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 200m,
            Accounts =
            [
                new Account { BeginningBalance = 10000m, WithdrawalPriority = 1 },
                new Account { Type = AccountType.Inherited, BeginningBalance = 10000m, WithdrawalPriority = 2 }
            ]
        };
        var originalJson = scenario.Serialize();
        var calculator = new PlanCalculator();
        var current = calculator.CalculateCurrentTrajectory(scenario);
        var maximum = calculator.CalculateMaximumSpend(scenario);
        var perpetual = calculator.CalculatePerpetualSpend(scenario);

        Assert.Equal(19600m, current.PlanYears[^1].EndingBalance);
        Assert.Equal(9000m, maximum.AnnualExpenses);
        Assert.Equal(0m, perpetual.AnnualExpenses);
        foreach (var plan in new[] { current, maximum, perpetual })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(2, plan.PlanYears.Count);
            Assert.All(plan.PlanYears, year => Assert.Equal(year.Accounts.Count, year.Incomes.Count));
            Assert.Equal(1500m, plan.PlanYears[0].Incomes[1].MandatoryAmount);
            Assert.Equal(20000m - plan.PlanYears.Sum(year => year.TotalExpenses),
                plan.PlanYears[^1].EndingBalance);
        }
        Assert.Equal(originalJson, scenario.Serialize());
    }

    [Fact]
    public void FromScenarioCreatesOneIncomePerOwnedAccount()
    {
        var accounts = Enum.GetValues<AccountType>()
            .Select(type => new Account { Type = type, BeginningBalance = 1000m })
            .ToArray();
        var year = CreateYear(0m, accounts);

        Assert.Equal(accounts.Length, year.Incomes.Count);
        for (var index = 0; index < accounts.Length; index++)
        {
            var income = year.Incomes[index];
            Assert.Same(year.Accounts[index], income.Account);
            Assert.NotSame(accounts[index], income.Account);
            Assert.Equal(accounts[index].Type == AccountType.Inherited ? 150m : 0m,
                income.MandatoryAmount);
            Assert.Equal(0m, income.VoluntaryAmount);
        }
    }

    [Fact]
    public void MandatoryWithdrawalsPrecedeWithdrawalPriority()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Assert.Equal(0m, Apply(year));
        Assert.Equal(950m, year.Accounts[0].EndingBalance);
        Assert.Equal(850m, year.Accounts[1].EndingBalance);
        Assert.Equal(50m, year.Incomes[0].VoluntaryAmount);
        Assert.Equal(150m, year.Incomes[1].MandatoryAmount);
        Assert.Equal(0m, year.Incomes[1].VoluntaryAmount);
        Assert.Equal(200m, year.Incomes.Sum(income => income.Amount));
    }

    [Fact]
    public void MandatoryAmountUsesBeginningBalanceBeforeAppreciation()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });
        year.Accounts[1].AppreciationRate = 0.1m;

        Apply(year);

        Assert.Equal(150m, year.Incomes[1].MandatoryAmount);
        Assert.Equal(950m, year.Accounts[1].EndingBalance);
        Assert.Equal(950m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void InheritedAccountsCanAlsoProvideVoluntaryIncome()
    {
        var year = CreateYear(300m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Apply(year);

        Assert.Equal(150m, year.Incomes[0].MandatoryAmount);
        Assert.Equal(150m, year.Incomes[0].VoluntaryAmount);
        Assert.Equal(700m, year.Accounts[0].EndingBalance);
        Assert.Equal(1000m, year.Accounts[1].EndingBalance);
    }

    [Theory]
    [InlineData(100, 850, 1350, 0)]
    [InlineData(500, 800, 1000, 50)]
    public void EveryMandatoryWithdrawalOccursBeforeVoluntaryIncome(
        int expenses, int firstEndingBalance, int brokerageEndingBalance, int voluntary)
    {
        var year = CreateYear(expenses,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 2 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 2000m, WithdrawalPriority = 99 });

        Assert.Equal(0m, Apply(year));
        Assert.Equal(450m, year.Incomes.Sum(income => income.MandatoryAmount));
        Assert.Equal((decimal)voluntary, year.Incomes.Sum(income => income.VoluntaryAmount));
        Assert.Equal((decimal)firstEndingBalance, year.Accounts[0].EndingBalance);
        Assert.Equal((decimal)brokerageEndingBalance, year.Accounts[1].EndingBalance);
        Assert.Equal(1700m, year.Accounts[2].EndingBalance);
        Assert.Equal(4000m - expenses, year.EndingBalance);
    }

    [Fact]
    public void SurplusGoesToFirstBrokerageByWithdrawalPriority()
    {
        var year = CreateYear(100m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 9 },
            new Account { Type = AccountType.Retirement, BeginningBalance = 500m, WithdrawalPriority = 0 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 500m, WithdrawalPriority = 2 });

        Apply(year);

        Assert.Equal(1000m, year.Accounts[0].EndingBalance);
        Assert.Equal(500m, year.Accounts[1].EndingBalance);
        Assert.Equal(850m, year.Accounts[2].EndingBalance);
        Assert.Equal(550m, year.Accounts[3].EndingBalance);
        Assert.All(year.Incomes, income => Assert.Equal(0m, income.VoluntaryAmount));
        Assert.Equal(2900m, year.EndingBalance);
    }

    [Fact]
    public void MandatoryIncomeIsWithdrawnAndTransferredEvenWithoutExpenses()
    {
        var year = CreateYear(0m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m },
            new Account { BeginningBalance = 0m });

        Apply(year);

        Assert.Equal(850m, year.Accounts[0].EndingBalance);
        Assert.Equal(150m, year.Accounts[1].EndingBalance);
        Assert.Equal(1000m, year.EndingBalance);
    }

    [Fact]
    public void ForecastFromRecalculatesMandatoryIncomeAndResetsVoluntaryIncome()
    {
        var first = CreateYear(300m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });
        Apply(first);

        var next = PlanYear.ForecastFrom(first);

        Assert.Equal(2, next.Incomes.Count);
        Assert.Equal(850m, next.Accounts[1].BeginningBalance);
        Assert.Equal(128m, next.Incomes[1].MandatoryAmount);
        Assert.All(next.Incomes, income => Assert.Equal(0m, income.VoluntaryAmount));
        Assert.Same(next.Accounts[1], next.Incomes[1].Account);
        Assert.NotSame(first.Incomes[1], next.Incomes[1]);
        Apply(next);
        Assert.Equal(150m, first.Incomes[1].MandatoryAmount);
        Assert.Equal(150m, first.Incomes[0].VoluntaryAmount);
        Assert.Equal(850m, first.Accounts[0].EndingBalance);
        Assert.Equal(300m, next.Incomes.Sum(income => income.Amount));
    }

    [Fact]
    public void MissingBrokerageIsReportedBeforeAnyWithdrawals()
    {
        var year = CreateYear(100m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m });
        year.Accounts[0].Appreciate();

        var exception = Assert.Throws<InvalidOperationException>(() => year.ApplyExpenses());

        Assert.Contains("Brokerage", exception.Message);
        Assert.Equal(1000m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void MandatoryIncomeIsCappedByAvailableFunds()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });
        year.Accounts[1].AppreciationRate = -0.9m;

        Assert.Equal(0m, Apply(year));
        Assert.Equal(100m, year.Incomes[1].MandatoryAmount);
        Assert.Equal(0m, year.Accounts[1].EndingBalance);
        Assert.Equal(100m, year.Incomes[0].VoluntaryAmount);
        Assert.Equal(900m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void UnfundedExpensesAreReturnedAfterAllIncomeIsUsed()
    {
        var year = CreateYear(1500m,
            new Account { BeginningBalance = 100m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Assert.Equal(400m, Apply(year));
        Assert.Equal(0m, year.EndingBalance);
        Assert.Equal(1100m, year.Incomes.Sum(income => income.Amount));
    }

    private static PlanYear CreateYear(decimal annualExpenses, params Account[] accounts)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = annualExpenses,
            Accounts = accounts.ToList()
        };
        return PlanYear.FromScenario(scenario, annualExpenses);
    }

    private static decimal Apply(PlanYear year)
    {
        foreach (var account in year.Accounts)
        {
            account.Appreciate();
        }
        return year.ApplyExpenses();
    }
}
