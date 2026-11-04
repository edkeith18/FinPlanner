using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ScheduledTransferTests
{
    [Fact]
    public void ScheduledTransfersUseInclusiveAgesAndOriginalCompoundingBaseline()
    {
        var scenario = CreateScenario();
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 100m, AgeStart = 59, AgeEnd = 60,
            AnnualRateOfIncrease = 0.1m, UseInflationValue = false
        });
        var original = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Equal(new[] { 0m, 110m, 121m, 0m }, plan.PlanYears
            .Select(year => year.Transfers.Where(t => t.Reason == TransferReason.Scheduled).Sum(t => t.Amount)));
        Assert.Equal(9769m, plan.PlanYears[^1].Accounts[0].EndingBalance);
        Assert.Equal(231m, plan.PlanYears[^1].Accounts[1].EndingBalance);
        Assert.All(plan.PlanYears, year =>
        {
            Assert.Equal(10000m, year.EndingBalance);
            Assert.Equal(0m, year.TotalCashFlow); // Internal transfers are not spendable income.
        });
        var movement = Assert.Single(plan.PlanYears[1].Transfers);
        Assert.Same(plan.PlanYears[1].Accounts[0], movement.FromAccount);
        Assert.Same(plan.PlanYears[1].Accounts[1], movement.ToAccount);
        Assert.Equal("Destination", movement.Name);
        Assert.Equal(original, scenario.Serialize());
        Assert.Equal(original, Scenario.Deserialize(original).Serialize());
    }

    [Fact]
    public void InflationLinkedTransferUsesScenarioInflationAndJsonCustomRatesArePercentages()
    {
        var scenario = CreateScenario();
        scenario.AnnualInflationRate = 4m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59,
            AnnualRateOfIncrease = 0.9m, UseInflationValue = true
        });
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59,
            AnnualRateOfIncrease = 0.1m, UseInflationValue = false
        });
        var loaded = Scenario.Deserialize(scenario.Serialize());
        Assert.Equal(0.1m, loaded.Transfers[1].AnnualRateOfIncrease);
        var plan = new PlanCalculator().CalculateCurrentTrajectory(loaded);
        Assert.Equal(104m, plan.PlanYears[1].Transfers.Where(t => t.Reason == TransferReason.Scheduled).ElementAt(0).Amount);
        Assert.Equal(110m, plan.PlanYears[1].Transfers.Where(t => t.Reason == TransferReason.Scheduled).ElementAt(1).Amount);
    }

    [Fact]
    public void RmdScheduledAndReinvestmentAreSeparateMovements()
    {
        var scenario = CreateScenario();
        scenario.DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-75);
        scenario.LifeExpectancy = 76;
        scenario.Accounts =
        [
            new Account { Name = "IRA", Type = AccountType.Retirement, BeginningBalance = 24600m },
            new Account { Name = "Other IRA", Type = AccountType.Retirement },
            new Account { Name = "Brokerage", Type = AccountType.Brokerage }
        ];
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Other IRA",
            Amount = 500m, AgeStart = 75, AgeEnd = 75
        });
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        Assert.Equal(3, year.Transfers.Count);
        Assert.Equal(1000m, year.Transfers.Single(t => t.Reason == TransferReason.Rmd).Amount);
        Assert.Equal(500m, year.Transfers.Single(t => t.Reason == TransferReason.Scheduled).Amount);
        Assert.Equal(1000m, year.Transfers.Single(t => t.Reason == TransferReason.SurplusReinvestment).Amount);
        Assert.Equal(23100m, year.Accounts[0].EndingBalance);
        Assert.Equal(500m, year.Accounts[1].EndingBalance);
        Assert.Equal(1000m, year.Accounts[2].EndingBalance);
        Assert.Equal(24600m, year.EndingBalance);
        Assert.Equal(1000m, year.TotalCashFlow);
        Assert.Equal(0m, year.AccountEarnings.Sum(e => e.Amount));
    }

    [Theory]
    [InlineData(AccountType.Retirement, 58)]
    [InlineData(AccountType.Inherited, 58)]
    [InlineData(AccountType.Retirement, 59)]
    [InlineData(AccountType.Inherited, 59)]
    public void ScheduledRothConversionsAreRejectedInEveryPlanningMode(AccountType sourceType, int age)
    {
        var scenario = CreateScenario();
        scenario.Accounts =
        [
            new Account { Name = "IRA", Type = sourceType, BeginningBalance = 1000000m },
            new Account { Name = "Roth", Type = AccountType.RetirementRoth },
            new Account { Name = "Brokerage", Type = AccountType.Brokerage }
        ];
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Roth",
            Amount = 1000m, AgeStart = age, AgeEnd = age
        });
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        Assert.Contains("Roth conversions", Assert.Throws<NotSupportedException>(
            () => calculator.CalculateCurrentTrajectory(scenario)).Message);
        Assert.Throws<NotSupportedException>(() => calculator.CalculateMaximumSpend(scenario));
        Assert.Throws<NotSupportedException>(() => calculator.CalculatePerpetualSpend(scenario));
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public void UnfundedScheduledTransferFailsPlanAndPreservesRequestedAndActualAmounts()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 100m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 200m, AgeStart = 58, AgeEnd = 58
        });
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Contains("Scheduled transfers", plan.FailureReason);
        var year = Assert.Single(plan.PlanYears);
        Assert.Equal(200m, year.Transfers.Sum(t => t.RequestedAmount));
        Assert.Equal(100m, year.Transfers.Sum(t => t.Amount));
        Assert.Equal(0m, year.Transfers.Single(t => t.FromAccount is null).Amount);
        Assert.Equal(100m, year.UnfundedTransfers);
        Assert.Equal(0m, year.UnfundedExpenses);
        Assert.False(year.IsFullyFunded);
        Assert.Equal(100m, year.EndingBalance);
    }

    [Fact]
    public void OnlyActiveInstructionsRequireValidUniqueAccountNames()
    {
        var scenario = CreateScenario();
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Missing",
            Amount = 100m, AgeStart = 50, AgeEnd = 57
        });
        Assert.True(new PlanCalculator().CalculateCurrentTrajectory(scenario).IsSuccessful);
        scenario.Transfers[0].AgeEnd = 58;
        Assert.Contains("Missing", Assert.Throws<InvalidOperationException>(() =>
            new PlanCalculator().CalculateCurrentTrajectory(scenario)).Message);
        scenario.Transfers[0].Name = "Destination";
        scenario.Accounts.Add(new Account { Name = "Destination" });
        Assert.Throws<InvalidOperationException>(() => new PlanCalculator().CalculateCurrentTrajectory(scenario));
    }

    [Fact]
    public void FinitePlanningModesApplyScheduledTransfersWithoutChangingInputs()
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59
        });
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        foreach (var plan in new[] { calculator.CalculateCurrentTrajectory(scenario),
            calculator.CalculateMaximumSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(new[] { 100m, 100m, 0m, 0m }, plan.PlanYears.Select(year =>
                year.Transfers.Where(t => t.Reason == TransferReason.Scheduled).Sum(t => t.Amount)));
        }
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public void PerpetualGrowthTargetOnlyConsidersObligationsWithinForecast()
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Destination",
            Amount = 1000000m, AgeStart = 65, AgeEnd = 65
        });
        Assert.True(new PlanCalculator().CalculatePerpetualSpend(scenario).IsSuccessful);
        scenario.LifeExpectancy = 66;
        Assert.False(new PlanCalculator().CalculatePerpetualSpend(scenario).IsSuccessful);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("maximum")]
    [InlineData("perpetual")]
    public void TransfersUsePriorityAcrossSourcesSkipDestinationAndContinueAfterDepletion(string mode)
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Accounts =
        [
            new Account { Name = "Second", Type = AccountType.Hsa, BeginningBalance = 1000000m, WithdrawalPriority = 2 },
            new Account { Name = "Destination", Type = AccountType.Hsa, BeginningBalance = 100000m, WithdrawalPriority = 0 },
            new Account { Name = "First", Type = AccountType.Hsa, BeginningBalance = 100m, WithdrawalPriority = 1 }
        ];
        scenario.Transfers.Add(new ScheduledTransfer { Name = "Destination", Amount = 1000m, AgeStart = 58, AgeEnd = 61 });
        var loaded = Scenario.Deserialize(scenario.Serialize());
        var calculator = new PlanCalculator();
        var plan = mode switch
        {
            "current" => calculator.CalculateCurrentTrajectory(loaded),
            "maximum" => calculator.CalculateMaximumSpend(loaded),
            _ => calculator.CalculatePerpetualSpend(loaded)
        };
        Assert.True(plan.IsSuccessful);
        Assert.Equal(4, plan.PlanYears.Count);
        var first = plan.PlanYears[0].Transfers.Where(t => t.Reason == TransferReason.Scheduled).ToList();
        Assert.Equal(new[] { "First", "Second" }, first.Select(t => t.FromAccount!.Name));
        Assert.Equal(new[] { 110m, 890m }, first.Select(t => t.Amount));
        Assert.All(plan.PlanYears, year =>
        {
            var transfers = year.Transfers.Where(t => t.Reason == TransferReason.Scheduled).ToList();
            Assert.Equal(1000m, transfers.Sum(t => t.RequestedAmount));
            Assert.Equal(1000m, transfers.Sum(t => t.Amount));
            Assert.All(transfers, t => Assert.NotEqual(t.ToAccount!.Id, t.FromAccount!.Id));
            Assert.Equal(0m, year.UnfundedTransfers);
        });
        Assert.All(plan.PlanYears.Skip(1), year =>
            Assert.All(year.Transfers.Where(t => t.Reason == TransferReason.Scheduled), t => Assert.Equal("Second", t.FromAccount!.Name)));
        Assert.DoesNotContain("FromAccountName", loaded.Serialize());
        Assert.DoesNotContain("ToAccountName", loaded.Serialize());
    }

    [Theory]
    [InlineData(250000, 250000, 50000, 0)]
    [InlineData(600000, 500000, 300000, 100000)]
    public void SplitFundingPreservesEachSourcesTaxTreatmentAndBasis(int requested, int funded, int ordinary, int unfunded)
    {
        var brokerage = new Account { Name = "Equities", BeginningBalance = 200000m, EndingBalance = 200000m, Basis = 120000m, WithdrawalPriority = 1 };
        var ira = new Account { Name = "IRA", Type = AccountType.Retirement, BeginningBalance = 300000m, EndingBalance = 300000m, WithdrawalPriority = 2 };
        var hsa = new Account { Name = "HSA", Type = AccountType.Hsa, BeginningBalance = 500000m, EndingBalance = 500000m, WithdrawalPriority = 0 };
        var year = new PlanYear
        {
            StartingYear = 2026,
            Accounts = [ira, hsa, brokerage],
            Transfers = [new AccountTransfer("HSA", null, hsa, requested, TransferReason.Scheduled)]
        };
        PlanYearCalculator.ApplyRmdsAndScheduledTransfers(year);
        Assert.Equal(unfunded == 0 ? 2 : 3, year.Transfers.Count);
        Assert.Equal(500000m + funded, hsa.EndingBalance);
        Assert.Equal(0m, brokerage.Basis);
        Assert.Equal(80000m, year.Transfers[0].RealizedGain);
        var tax = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(80000m, tax.LongTermCapitalGains);
        Assert.Equal((decimal)ordinary, tax.OrdinaryIncome);
        Assert.Equal((decimal)funded, tax.HsaContribution);
        Assert.Equal((decimal)unfunded, year.UnfundedTransfers);
        Assert.Equal(1000000m, year.EndingBalance);
    }

    [Fact]
    public void DestinationBalanceCannotFundItsOwnTransfer()
    {
        var scenario = CreateScenario();
        scenario.Accounts.RemoveAt(0);
        scenario.Accounts[0].BeginningBalance = 10000m;
        scenario.Transfers.Add(new ScheduledTransfer { Name = "Destination", Amount = 100m, AgeStart = 58, AgeEnd = 58 });
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Equal(100m, plan.PlanYears[0].UnfundedTransfers);
        Assert.Equal(10000m, plan.PlanYears[0].EndingBalance);
        Assert.Equal(0m, Assert.Single(plan.PlanYears[0].Transfers).Amount);
    }

    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58), LifeExpectancy = 62,
        Accounts =
        [
            new Account { Name = "Source", Type = AccountType.Hsa, BeginningBalance = 10000m, WithdrawalPriority = 3 },
            new Account { Name = "Destination", Type = AccountType.Hsa, WithdrawalPriority = 5 }
        ]
    };
}
