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
            Name = "Move funds", FromAccountName = "Source", ToAccountName = "Destination",
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
        Assert.Equal("Move funds", movement.Name);
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
            Name = "Inflation linked", FromAccountName = "Source", ToAccountName = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59,
            AnnualRateOfIncrease = 0.9m, UseInflationValue = true
        });
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Custom", FromAccountName = "Source", ToAccountName = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59,
            AnnualRateOfIncrease = 0.1m, UseInflationValue = false
        });
        var loaded = Scenario.Deserialize(scenario.Serialize());
        Assert.Equal(0.1m, loaded.Transfers[1].AnnualRateOfIncrease);
        var plan = new PlanCalculator().CalculateCurrentTrajectory(loaded);
        Assert.Equal(104m, plan.PlanYears[1].Transfers.Single(t => t.Name == "Inflation linked").Amount);
        Assert.Equal(110m, plan.PlanYears[1].Transfers.Single(t => t.Name == "Custom").Amount);
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
            new Account { Name = "Roth", Type = AccountType.RetirementRoth },
            new Account { Name = "Brokerage", Type = AccountType.Brokerage }
        ];
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Conversion", FromAccountName = "IRA", ToAccountName = "Roth",
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

    [Fact]
    public void UnfundedScheduledTransferFailsPlanAndPreservesRequestedAndActualAmounts()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 100m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Too much", FromAccountName = "Source", ToAccountName = "Destination",
            Amount = 200m, AgeStart = 58, AgeEnd = 58
        });
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Contains("Scheduled transfers", plan.FailureReason);
        var year = Assert.Single(plan.PlanYears);
        var transfer = Assert.Single(year.Transfers);
        Assert.Equal(200m, transfer.RequestedAmount);
        Assert.Equal(100m, transfer.Amount);
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
            Name = "Expired", FromAccountName = "Missing", ToAccountName = "Destination",
            Amount = 100m, AgeStart = 50, AgeEnd = 57
        });
        Assert.True(new PlanCalculator().CalculateCurrentTrajectory(scenario).IsSuccessful);
        scenario.Transfers[0].AgeEnd = 58;
        Assert.Contains("Missing", Assert.Throws<InvalidOperationException>(() =>
            new PlanCalculator().CalculateCurrentTrajectory(scenario)).Message);
        scenario.Transfers[0].FromAccountName = "Source";
        scenario.Accounts.Add(new Account { Name = "Source" });
        Assert.Throws<InvalidOperationException>(() => new PlanCalculator().CalculateCurrentTrajectory(scenario));
    }

    [Fact]
    public void AllPlanningModesApplyScheduledTransfersWithoutChangingInputs()
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Scheduled", FromAccountName = "Source", ToAccountName = "Destination",
            Amount = 100m, AgeStart = 58, AgeEnd = 59
        });
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        foreach (var plan in new[] { calculator.CalculateCurrentTrajectory(scenario),
            calculator.CalculateMaximumSpend(scenario), calculator.CalculatePerpetualSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            Assert.Equal(new[] { 100m, 100m, 0m, 0m }, plan.PlanYears.Select(year =>
                year.Transfers.Where(t => t.Reason == TransferReason.Scheduled).Sum(t => t.Amount)));
        }
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public void PerpetualProofWaitsForFutureScheduledObligations()
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Transfers.Add(new ScheduledTransfer
        {
            Name = "Impossible future obligation", FromAccountName = "Source", ToAccountName = "Destination",
            Amount = 1000000m, AgeStart = 65, AgeEnd = 65
        });
        Assert.False(new PlanCalculator().CalculatePerpetualSpend(scenario).IsSuccessful);
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
