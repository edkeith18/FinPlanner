using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class AccountTransferTests
{
    [Theory]
    [InlineData(AccountType.Retirement, 74, 24600, 0)]
    [InlineData(AccountType.Retirement, 75, 24600, 1000)]
    [InlineData(AccountType.Inherited, 51, 34300, 0)]
    [InlineData(AccountType.Inherited, 52, 34300, 1000)]
    [InlineData(AccountType.RetirementRoth, 75, 24600, 0)]
    [InlineData(AccountType.RetirementRoth, 100, 24600, 0)]
    public void MandatoryDistributionsAreTransfersAndNeverInvestmentIncome(
        AccountType type, int age, int balance, int expected)
    {
        var account = new Account { Type = type, BeginningBalance = balance };
        var transfer = PlanYearCalculator.CreateRmdTransfer(account, age);
        Assert.Same(account, transfer.FromAccount);
        Assert.Null(transfer.ToAccount);
        Assert.Equal(TransferReason.Rmd, transfer.Reason);
        Assert.Equal((decimal)expected, transfer.MandatoryAmount);
        Assert.Equal(0m, transfer.VoluntaryAmount);
        Assert.Equal((decimal)expected, transfer.Amount);
        Assert.Equal(0m, PlanYearCalculator.CalculateAccountEarnings(account).Amount);
    }

    [Theory]
    [InlineData(AccountType.Retirement)]
    [InlineData(AccountType.RetirementRoth)]
    [InlineData(AccountType.Inherited)]
    public void RetirementWithdrawalsFundExpensesAsTransfers(AccountType type)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-75),
            LifeExpectancy = 76,
            AnnualExpenses = 5000m,
            Accounts = [new Account { Type = type, BeginningBalance = 24600m }]
        };
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        var year = plan.PlanYears[0];
        Assert.Equal(0m, year.AccountEarnings[0].Amount);
        Assert.Equal(5000m, year.Transfers.Sum(transfer => transfer.Amount));
        Assert.Equal(19600m, year.EndingBalance);
        Assert.Equal(5000m, year.TotalCashFlow);
    }
}
