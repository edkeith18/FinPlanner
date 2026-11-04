using System.Text.Json;
using System.Text.Json.Nodes;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class AccountBasisTests
{
    [Fact]
    public async Task BasisIsReadByBothJsonReadersAndPreservedInSnapshotsAndJson()
    {
        var json = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            Accounts = [new Account { BeginningBalance = 100000m, Basis = 60000.75m }]
        }.Serialize();
        var scenario = Scenario.Deserialize(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var asyncScenario = await Scenario.DeserializeAsync(stream);
        Assert.Equal(60000.75m, scenario.Accounts[0].Basis);
        Assert.Equal(60000.75m, asyncScenario.Accounts[0].Basis);
        Assert.Equal(60000.75m, new Scenario(scenario).Accounts[0].Basis);
        Assert.Equal(60000.75m, JsonNode.Parse(scenario.Serialize())!["Accounts"]![0]!["Basis"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData("Equities", "missing")]
    [InlineData("Bonds", "missing")]
    [InlineData("Equities", "null")]
    [InlineData("Bonds", "null")]
    [InlineData("Equities", "negative")]
    public async Task BothJsonReadersRejectBrokerageWithoutValidBasis(string holdings, string invalid)
    {
        var document = JsonNode.Parse(new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            Accounts = [new Account { Name = "Brokerage", Basis = 0m }]
        }.Serialize())!;
        var account = document["Accounts"]![0]!.AsObject();
        account["Holdings"] = holdings;
        if (invalid == "missing") account.Remove("Basis");
        else if (invalid == "null") account["Basis"] = null;
        else account["Basis"] = -1;
        var json = document.ToJsonString();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.Throws<JsonException>(() => Scenario.Deserialize(json));
        await Assert.ThrowsAsync<JsonException>(() => Scenario.DeserializeAsync(stream));
    }

    [Theory]
    [InlineData(AccountType.Brokerage)]
    [InlineData(AccountType.Hsa)]
    [InlineData(AccountType.Retirement)]
    [InlineData(AccountType.RetirementRoth)]
    [InlineData(AccountType.Inherited)]
    public async Task BothJsonReadersAcceptZeroBasisAndNonBrokerageOmission(AccountType type)
    {
        var document = JsonNode.Parse(new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            Accounts = [new Account { Type = type, Basis = 0m }]
        }.Serialize())!;
        if (type != AccountType.Brokerage) document["Accounts"]![0]!.AsObject().Remove("Basis");
        var json = document.ToJsonString();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.Equal(0m, Scenario.Deserialize(json).Accounts[0].Basis);
        Assert.Equal(0m, (await Scenario.DeserializeAsync(stream)).Accounts[0].Basis);
    }

    [Theory]
    [InlineData(AccountHoldings.Equities)]
    [InlineData(AccountHoldings.Bonds)]
    public void RepeatedWithdrawalsRemoveProportionalBasisAndClassifyOnlyGains(AccountHoldings holdings)
    {
        var source = new Account { Holdings = holdings, BeginningBalance = 100000m, EndingBalance = 100000m, Basis = 60000m };
        var first = new AccountTransfer("Sale", source, null, 10000m, TransferReason.ExpenseWithdrawal);
        var second = new AccountTransfer("Sale", source, null, 20000m, TransferReason.ExpenseWithdrawal);
        PlanYearCalculator.ApplyTransfer(first);
        PlanYearCalculator.ApplyTransfer(second);
        Assert.Equal(4000m, first.RealizedGain);
        Assert.Equal(8000m, second.RealizedGain);
        Assert.Equal(42000m, source.Basis);
        var taxes = TaxCalculator.Calculate(new PlanYear { StartingYear = 2026, Transfers = [first, second] }, TaxInfo.Default);
        Assert.Equal(holdings == AccountHoldings.Equities ? 12000m : 0m, taxes.LongTermCapitalGains);
        Assert.Equal(holdings == AccountHoldings.Bonds ? 12000m : 0m, taxes.OrdinaryIncome);
    }

    [Theory]
    [InlineData(0, 10000, 0)]
    [InlineData(100000, 0, 90000)]
    [InlineData(125000, 0, 112500)]
    public void ZeroBasisAndLossPositionsUseNonnegativeTaxableGain(int basis, int gain, int remainingBasis)
    {
        var source = new Account { EndingBalance = 100000m, Basis = basis };
        var sale = new AccountTransfer("Sale", source, null, 10000m, TransferReason.ExpenseWithdrawal);
        PlanYearCalculator.ApplyTransfer(sale);
        Assert.Equal((decimal)gain, sale.RealizedGain);
        Assert.Equal((decimal)remainingBasis, source.Basis);
    }

    [Fact]
    public void AppreciationDoesNotIncreaseBasis()
    {
        var account = new Account { BeginningBalance = 100000m, Basis = 60000m, AppreciationRate = 0.2m };
        PlanYearCalculator.AppreciateAccounts(new PlanYear { Accounts = [account] });
        Assert.Equal(60000m, account.Basis);
        var sale = new AccountTransfer("Sale", account, null, 10000m, TransferReason.ExpenseWithdrawal);
        PlanYearCalculator.ApplyTransfer(sale);
        Assert.Equal(5000m, sale.RealizedGain);
        Assert.Equal(55000m, account.Basis);
    }

    [Fact]
    public void CappedWithdrawalConsumesOnlyAvailableBalanceAndClearsBasis()
    {
        var source = new Account { EndingBalance = 100000m, Basis = 60000m };
        var sale = new AccountTransfer("Sale", source, null, 200000m, TransferReason.ExpenseWithdrawal);
        PlanYearCalculator.ApplyTransfer(sale);
        Assert.Equal(100000m, sale.Amount);
        Assert.Equal(40000m, sale.RealizedGain);
        Assert.Equal(0m, source.Basis);
        Assert.Equal(0m, source.EndingBalance);
    }

    [Fact]
    public void ScheduledTransfersRealizeSourceGainAndAddPurchaseBasisToDestination()
    {
        var source = new Account { EndingBalance = 100000m, Basis = 60000m };
        var destination = new Account { EndingBalance = 20000m, Basis = 15000m };
        var movement = new AccountTransfer("Transfer", source, destination, 10000m, TransferReason.Scheduled);
        PlanYearCalculator.ApplyTransfer(movement);
        Assert.Equal(4000m, movement.RealizedGain);
        Assert.Equal(54000m, source.Basis);
        Assert.Equal(25000m, destination.Basis);
        Assert.Equal(30000m, destination.EndingBalance);
    }

    [Fact]
    public void SurplusReinvestmentAddsBasisWithoutRealizingGain()
    {
        var destination = new Account { EndingBalance = 100000m, Basis = 60000m };
        var deposit = new AccountTransfer("Surplus", null, destination, 10000m, TransferReason.SurplusReinvestment);
        PlanYearCalculator.ApplyTransfer(deposit);
        Assert.Equal(70000m, destination.Basis);
        Assert.Null(deposit.RealizedGain);
        Assert.Equal(0m, TaxCalculator.Calculate(new PlanYear { Transfers = [deposit] }, TaxInfo.Default).LongTermCapitalGains);
    }

    [Fact]
    public void TaxFundingTrialsPreserveBasisAndForecastCarriesRemainingBasis()
    {
        var scenario = new Scenario
        {
            StartYear = 2026,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 61,
            FirstYearAnnualExpenses = 200000m,
            Accounts = [new Account { BeginningBalance = 2000000m, Basis = 1000000m }]
        };
        var json = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        decimal basis = 1000000m;
        foreach (var year in plan.PlanYears)
        {
            var account = year.Accounts.Single();
            foreach (var transfer in year.Transfers)
            {
                if (transfer.FromAccount?.Id == account.Id)
                    basis -= transfer.Amount - transfer.RealizedGain!.Value;
                if (transfer.ToAccount?.Id == account.Id)
                    basis += transfer.Amount;
            }
            Assert.Equal(basis, account.Basis);
            if (year.Taxes is not null)
                Assert.Equal(decimal.Round(year.Transfers.Where(t => t.FromAccount?.Id == account.Id).Sum(t => t.RealizedGain ?? 0m), 4), year.Taxes.LongTermCapitalGains);
        }
        Assert.True(plan.PlanYears[1].TaxExpense > 0m);
        Assert.Equal(json, scenario.Serialize());
        var repeat = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.Equal(plan.PlanYears[^1].Accounts[0].Basis, repeat.PlanYears[^1].Accounts[0].Basis);
    }
}
