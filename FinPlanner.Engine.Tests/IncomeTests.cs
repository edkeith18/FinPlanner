using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class IncomeTests
{
    [Theory]
    [InlineData("InitialAmount")]
    [InlineData("Amount")]
    public void IncomeJsonReadsBothAmountNamesAndWritesPercentageRates(string amountName)
    {
        var scenario = Scenario.Deserialize($$"""
            { "Accounts": [{}], "Income": [{ "Name": "Social Security", "{{amountName}}": 71208,
              "AgeStart": 67, "AnnualRateOfIncrease": 4.8 }] }
            """);
        var income = Assert.Single(scenario.Income);
        Assert.Equal(71208m, income.InitialAmount);
        Assert.Equal(71208m, income.Amount);
        Assert.Equal(67, income.AgeStart);
        Assert.Equal(0.048m, income.AnnualRateOfIncrease);
        using var json = JsonDocument.Parse(scenario.Serialize());
        var item = json.RootElement.GetProperty("Income")[0];
        Assert.Equal(71208m, item.GetProperty("InitialAmount").GetDecimal());
        Assert.Equal(4.8m, item.GetProperty("AnnualRateOfIncrease").GetDecimal());
        Assert.False(item.TryGetProperty("Amount", out _));
        Assert.Equal(0.048m, Scenario.Deserialize(scenario.Serialize()).Income[0].AnnualRateOfIncrease);
    }

    [Fact]
    public void FutureIncomeCompoundsDuringInactiveYearsWithoutChangingInputs()
    {
        var scenario = CreateScenario();
        scenario.LifeExpectancy = 64;
        scenario.Income.Add(new Income
        {
            Name = "Future", InitialAmount = 100.49m, AgeStart = 62, AnnualRateOfIncrease = 0.01m
        });
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var original = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.Equal(new[] { 0m, 0m, 103m, 104m }, plan.PlanYears.Select(year => year.ExternalIncome));
        var movement = plan.PlanYears[2].Transfers.Single(t => t.Reason == TransferReason.ExternalIncome);
        Assert.Null(movement.FromAccount);
        Assert.Null(movement.ToAccount);
        Assert.Equal("Future", movement.Name);
        Assert.Equal(103m, movement.RequestedAmount);
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public void IncomeFundsExpensesBeforeAccountWithdrawals()
    {
        var scenario = CreateScenario();
        scenario.AnnualExpenses = 1500m;
        scenario.Income.Add(new Income { InitialAmount = 1000m, AgeStart = 60 });
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        Assert.Equal(9500m, year.EndingBalance);
        Assert.Equal(1000m, year.ExternalIncome);
        Assert.Equal(1500m, year.TotalCashFlow);
        Assert.Equal(500m, year.Transfers.Single(t => t.Reason == TransferReason.ExpenseWithdrawal).Amount);
    }

    [Fact]
    public void IncomeDoesNotSuppressRmdAndSurplusIsReinvestedOnlyOnce()
    {
        var scenario = CreateScenario();
        scenario.DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-75);
        scenario.LifeExpectancy = 76;
        scenario.AnnualExpenses = 500m;
        scenario.Accounts = [new Account { Type = AccountType.Retirement, BeginningBalance = 24600m },
            new Account { Type = AccountType.Brokerage }];
        scenario.Income.Add(new Income { InitialAmount = 1000m, AgeStart = 75 });
        var year = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears[0];
        Assert.Equal(1000m, year.Transfers.Single(t => t.Reason == TransferReason.Rmd).Amount);
        Assert.Equal(1500m, year.Transfers.Single(t => t.Reason == TransferReason.SurplusReinvestment).Amount);
        Assert.Equal(2000m, year.TotalCashFlow);
        Assert.Equal(25100m, year.EndingBalance);
        Assert.DoesNotContain(year.Transfers, t => t.Reason == TransferReason.ExpenseWithdrawal);
    }

    [Fact]
    public void IncomeSurplusRequiresDestinationBeforeAnyWithdrawals()
    {
        var scenario = CreateScenario();
        scenario.Income.Add(new Income { InitialAmount = 1000m });
        Assert.Contains("Brokerage", Assert.Throws<InvalidOperationException>(() =>
            new PlanCalculator().CalculateCurrentTrajectory(scenario)).Message);
    }

    [Fact]
    public void IncomeCanFundAPlanWithoutAnyAccountBalance()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 0m;
        scenario.AnnualExpenses = 1000m;
        scenario.Income.Add(new Income { InitialAmount = 1000m });
        var calculator = new PlanCalculator();
        Assert.True(calculator.CalculateCurrentTrajectory(scenario).IsSuccessful);
        var maximum = calculator.CalculateMaximumSpend(scenario);
        Assert.True(maximum.IsSuccessful);
        Assert.Equal(1000m, maximum.AnnualExpenses);
        var perpetual = calculator.CalculatePerpetualSpend(scenario);
        Assert.True(perpetual.IsSuccessful);
        Assert.Equal(1000m, perpetual.AnnualExpenses);
    }

    [Fact]
    public void AllPlanningModesIncludeIncomeAndCopyAllInputFields()
    {
        var scenario = CreateScenario();
        scenario.AnnualExpenses = 2000m;
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Income.Add(new Income { Name = "Pension", InitialAmount = 1000m, AgeStart = 60 });
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        var plans = new[] { calculator.CalculateCurrentTrajectory(scenario),
            calculator.CalculateMaximumSpend(scenario), calculator.CalculatePerpetualSpend(scenario) };
        Assert.All(plans, plan =>
        {
            Assert.True(plan.IsSuccessful);
            Assert.All(plan.PlanYears, year => Assert.Equal(1000m, year.ExternalIncome));
        });
        Assert.Equal(2000m, plans[2].AnnualExpenses);
        Assert.Equal(original, scenario.Serialize());
        var snapshot = new Scenario(scenario);
        Assert.NotSame(scenario.Income[0], snapshot.Income[0]);
        Assert.Equal(60, snapshot.Income[0].AgeStart);
    }

    [Fact]
    public void DelayedIncomeDoesNotFundExpensesBeforeItStartsInAnyMode()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 500m;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        scenario.AnnualExpenses = 1000m;
        scenario.Income.Add(new Income { InitialAmount = 1000m, AgeStart = 62 });
        var calculator = new PlanCalculator();
        Assert.False(calculator.CalculateCurrentTrajectory(scenario).IsSuccessful);
        var maximum = calculator.CalculateMaximumSpend(scenario);
        var perpetual = calculator.CalculatePerpetualSpend(scenario);
        Assert.Equal(0m, maximum.AnnualExpenses);
        Assert.Equal(0m, perpetual.AnnualExpenses);
        Assert.All(maximum.PlanYears.Take(2), year => Assert.Equal(0m, year.ExternalIncome));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -0.1)]
    public void PerpetualCalculationRejectsNegativeIncomeAssumptions(int amount, double rate)
    {
        var scenario = CreateScenario();
        scenario.Income.Add(new Income { InitialAmount = amount, AnnualRateOfIncrease = (decimal)rate });
        Assert.Throws<NotSupportedException>(() => new PlanCalculator().CalculatePerpetualSpend(scenario));
    }

    [Fact]
    public void ExternalEndpointExceptionDoesNotAllowInvalidAccountTransfers()
    {
        Assert.Throws<ArgumentException>(() => new AccountTransfer("Invalid", null, null, 10m, TransferReason.Scheduled));
        Assert.Throws<ArgumentException>(() => new AccountTransfer("Invalid", new Account(), null, 10m, TransferReason.ExternalIncome));
    }

    [Fact]
    public void GrowingIncomeCanSupportGrowingSpendingWithoutInvestmentReturns()
    {
        var scenario = CreateScenario();
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage, Holdings = AccountHoldings.Bonds });
        scenario.AnnualInflationRate = 2m;
        scenario.Income.Add(new Income { InitialAmount = 2000m, AgeStart = 60, AnnualRateOfIncrease = 0.03m });
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.AnnualExpenses >= 2000m);
    }

    [Fact]
    public void SnapshotPreservesIncomeStartAndGrowthWithoutSharingObjects()
    {
        var scenario = CreateScenario();
        scenario.Income.Add(new Income { InitialAmount = 100m, AgeStart = 62, AnnualRateOfIncrease = 0.048m });
        var snapshot = new Scenario(scenario);
        scenario.Income[0].AgeStart = 60;
        scenario.Income[0].AnnualRateOfIncrease = 0m;
        Assert.Equal(62, snapshot.Income[0].AgeStart);
        Assert.Equal(0.048m, snapshot.Income[0].AnnualRateOfIncrease);
    }

    [Fact]
    public void IdenticalGrowingIncomeAndExpensesNeedNoInvestmentBalanceOrSurplusAccount()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 0m;
        scenario.AnnualExpenses = 1000m;
        scenario.AnnualInflationRate = 2m;
        scenario.Income.Add(new Income { InitialAmount = 1000m, AgeStart = 60, AnnualRateOfIncrease = 0.02m });
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Equal(1000m, plan.AnnualExpenses);
        Assert.All(plan.PlanYears, year => Assert.Equal(0m, year.EndingBalance));
    }

    [Fact]
    public void DelayedGrowingIncomeImprovesPerpetualSpendingAfterFundingTheBridgeYears()
    {
        var scenario = CreateScenario();
        scenario.LifeExpectancy = 85;
        scenario.SecuritiesAnnualRateOfReturn = 0.07m;
        scenario.AnnualInflationRate = 4m;
        scenario.Accounts[0].BeginningBalance = 1010000m;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var calculator = new PlanCalculator();
        var withoutIncome = calculator.CalculatePerpetualSpend(scenario);
        scenario.Income.Add(new Income
        {
            Name = "Social Security", InitialAmount = 71208m, AgeStart = 67, AnnualRateOfIncrease = 0.048m
        });
        var withIncome = calculator.CalculatePerpetualSpend(scenario);
        Assert.True(withIncome.IsSuccessful);
        Assert.True(withIncome.AnnualExpenses > withoutIncome.AnnualExpenses);
        Assert.All(withIncome.PlanYears.Where(year => year.StartingAge < 67), year => Assert.Equal(0m, year.ExternalIncome));
        Assert.True(withIncome.PlanYears.Single(year => year.StartingAge == 67).ExternalIncome > 71208m);
    }

    [Fact]
    public void PlanPreservesIncomeNamesIncludingSourcesBeyondItsReportingHorizon()
    {
        var scenario = CreateScenario();
        scenario.Income =
        [
            new Income { Name = "Future pension", InitialAmount = 100m, AgeStart = 90 },
            new Income { Name = "Future pension", InitialAmount = 200m, AgeStart = 91 },
            new Income { Name = "Other income", InitialAmount = 300m, AgeStart = 92 }
        ];
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        scenario.Income[0].Name = "Changed";
        scenario.Income.Clear();
        Assert.Equal(new[] { "Future pension", "Other income" }, plan.IncomeNames);
        Assert.All(plan.PlanYears, year => Assert.Equal(0m, year.ExternalIncome));
    }

    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-60),
        LifeExpectancy = 63,
        Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }]
    };
}
