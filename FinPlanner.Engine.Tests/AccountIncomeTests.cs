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

        Assert.Equal(19835m, current.PlanYears[^1].EndingBalance);
        Assert.Equal(10000m, maximum.AnnualExpenses);
        Assert.Equal(0m, perpetual.AnnualExpenses);
        foreach (var plan in new[] { current, maximum, perpetual })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(2, plan.PlanYears.Count);
            Assert.All(plan.PlanYears, year => Assert.Equal(year.Accounts.Count, year.AccountEarnings.Count));
            Assert.Equal(116m, plan.PlanYears[0].AccountEarnings[0].Amount);
            Assert.Equal(353m, plan.PlanYears[0].MandatoryFrom(1));
            Assert.Equal(20000m + plan.PlanYears.Sum(year => year.AccountEarnings.Sum(income => income.EarnedAmount))
                - plan.PlanYears.Sum(year => year.TotalExpenses),
                plan.PlanYears[^1].EndingBalance);
        }
        Assert.Equal(originalJson, scenario.Serialize());
    }

    [Fact]
    public void CalculateEarningsCreatesOneIncomePerOwnedAccount()
    {
        var accounts = Enum.GetValues<AccountType>()
            .Select(type => new Account { Type = type, BeginningBalance = 1000m })
            .ToArray();
        var year = CreateYear(0m, accounts);

        Assert.Empty(year.AccountEarnings);
        PlanYearCalculator.CalculateEarnings(year);

        Assert.Equal(accounts.Length, year.AccountEarnings.Count);
        for (var index = 0; index < accounts.Length; index++)
        {
            var income = year.AccountEarnings[index];
            Assert.Same(year.Accounts[index], income.Account);
            Assert.NotSame(accounts[index], income.Account);
            Assert.Equal(accounts[index].Type switch
                {
                    AccountType.Inherited => 0m,
                    AccountType.Brokerage => 12m,
                    _ => 0m
                },
                income.Amount);
            Assert.Equal(0m, year.VoluntaryFrom(index));
        }
    }

    [Fact]
    public void MandatoryWithdrawalsPrecedeWithdrawalPriority()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Apply(year);
        Assert.True(year.IsFullyFunded);
        Assert.Equal(0m, year.UnfundedExpenses);
        Assert.Equal(847m, year.Accounts[0].EndingBalance);
        Assert.Equal(965m, year.Accounts[1].EndingBalance);
        Assert.Equal(153m, year.VoluntaryFrom(0));
        Assert.Equal(35m, year.MandatoryFrom(1));
        Assert.Equal(0m, year.VoluntaryFrom(1));
        Assert.Equal(200m, year.TotalCashFlow);
    }

    [Fact]
    public void MandatoryAmountUsesBeginningBalanceBeforeAppreciation()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });
        year.Accounts[1].AppreciationRate = 0.1m;

        Apply(year);

        Assert.Equal(35m, year.MandatoryFrom(1));
        Assert.Equal(1065m, year.Accounts[1].EndingBalance);
        Assert.Equal(847m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void InheritedAccountsCanAlsoProvideVoluntaryIncome()
    {
        var year = CreateYear(300m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Apply(year);

        Assert.Equal(35m, year.MandatoryFrom(0));
        Assert.Equal(253m, year.VoluntaryFrom(0));
        Assert.Equal(712m, year.Accounts[0].EndingBalance);
        Assert.Equal(1000m, year.Accounts[1].EndingBalance);
    }

    [Theory]
    [InlineData(100, 965, 1018, 0)]
    [InlineData(500, 583, 1000, 382)]
    public void EveryMandatoryWithdrawalOccursBeforeVoluntaryIncome(
        int expenses, int firstEndingBalance, int brokerageEndingBalance, int voluntary)
    {
        var year = CreateYear(expenses,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 2 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 2000m, WithdrawalPriority = 99 });

        Apply(year);
        Assert.True(year.IsFullyFunded);
        Assert.Equal(0m, year.UnfundedExpenses);
        Assert.Equal(118m, year.MandatoryCashFlow);
        Assert.Equal((decimal)voluntary, year.Transfers.Sum(transfer => transfer.VoluntaryAmount));
        Assert.Equal((decimal)firstEndingBalance, year.Accounts[0].EndingBalance);
        Assert.Equal((decimal)brokerageEndingBalance, year.Accounts[1].EndingBalance);
        Assert.Equal(1929m, year.Accounts[2].EndingBalance);
        Assert.Equal(4012m - expenses, year.EndingBalance);
    }

    [Fact]
    public void SurplusGoesToFirstBrokerageByWithdrawalPriority()
    {
        var year = CreateYear(10m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 9 },
            new Account { Type = AccountType.Retirement, BeginningBalance = 500m, WithdrawalPriority = 0 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { BeginningBalance = 500m, WithdrawalPriority = 2 });

        Apply(year);

        Assert.Equal(1000m, year.Accounts[0].EndingBalance);
        Assert.Equal(500m, year.Accounts[1].EndingBalance);
        Assert.Equal(965m, year.Accounts[2].EndingBalance);
        Assert.Equal(543m, year.Accounts[3].EndingBalance);
        Assert.All(year.Transfers, transfer => Assert.Equal(0m, transfer.VoluntaryAmount));
        Assert.Equal(3008m, year.EndingBalance);
    }

    [Fact]
    public void MandatoryIncomeIsWithdrawnAndTransferredEvenWithoutExpenses()
    {
        var year = CreateYear(0m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m },
            new Account { BeginningBalance = 0m });

        Apply(year);

        Assert.Equal(965m, year.Accounts[0].EndingBalance);
        Assert.Equal(35m, year.Accounts[1].EndingBalance);
        Assert.Equal(1000m, year.EndingBalance);
    }

    [Fact]
    public void ForecastFromRecalculatesMandatoryIncomeAndResetsVoluntaryIncome()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            AnnualExpenses = 300m,
            Accounts =
            [
                new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
                new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 }
            ]
        };
        var first = PlanYearCalculator.FromScenario(scenario, 300m);
        Apply(first);

        var next = PlanYearCalculator.ForecastFrom(first, scenario, 300m);
        PlanYearCalculator.CalculateEarnings(next);

        Assert.Equal(2, next.AccountEarnings.Count);
        Assert.Equal(965m, next.Accounts[1].BeginningBalance);
        Assert.Equal(35m, next.MandatoryFrom(1));
        Assert.All(next.Transfers, transfer => Assert.Equal(0m, transfer.VoluntaryAmount));
        Assert.Same(next.Accounts[1], next.AccountEarnings[1].Account);
        Assert.NotSame(first.AccountEarnings[1], next.AccountEarnings[1]);
        Apply(next);
        Assert.Equal(35m, first.MandatoryFrom(1));
        Assert.Equal(253m, first.VoluntaryFrom(0));
        Assert.Equal(747m, first.Accounts[0].EndingBalance);
        Assert.Equal(300m, next.TotalCashFlow);
    }

    [Fact]
    public void MissingBrokerageIsReportedBeforeAnyWithdrawals()
    {
        var year = CreateYear(10m,
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m });
        PlanYearCalculator.AppreciateAccounts(year);

        PlanYearCalculator.CalculateEarnings(year);
        var exception = Assert.Throws<InvalidOperationException>(() => PlanYearCalculator.ApplyRmdsAndScheduledTransfers(year));

        Assert.Contains("Brokerage", exception.Message);
        Assert.Equal(1000m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void MandatoryIncomeIsCappedByAvailableFunds()
    {
        var year = CreateYear(200m,
            new Account { BeginningBalance = 1000m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });
        year.Accounts[1].AppreciationRate = -0.99m;

        Apply(year);
        Assert.True(year.IsFullyFunded);
        Assert.Equal(0m, year.UnfundedExpenses);
        Assert.Equal(10m, year.MandatoryFrom(1));
        Assert.Equal(0m, year.Accounts[1].EndingBalance);
        Assert.Equal(178m, year.VoluntaryFrom(0));
        Assert.Equal(822m, year.Accounts[0].EndingBalance);
    }

    [Fact]
    public void UnfundedExpensesAreRecordedAfterAllIncomeIsUsed()
    {
        var year = CreateYear(1500m,
            new Account { BeginningBalance = 100m, WithdrawalPriority = 1 },
            new Account { Type = AccountType.Inherited, BeginningBalance = 1000m, WithdrawalPriority = 2 });

        Apply(year);
        Assert.False(year.IsFullyFunded);
        Assert.Equal(399m, year.UnfundedExpenses);
        Assert.Equal(0m, year.EndingBalance);
        Assert.Equal(1101m, year.TotalCashFlow);
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
        return PlanYearCalculator.FromScenario(scenario, annualExpenses);
    }

    private static void Apply(PlanYear year)
    {
        PlanYearCalculator.Calculate(year);
    }
}
