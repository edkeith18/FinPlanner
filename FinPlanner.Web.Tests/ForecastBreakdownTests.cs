using FinPlanner.Engine;
using FinPlanner.Web.Services;

namespace FinPlanner.Web.Tests;

public class ForecastBreakdownTests
{
    [Fact]
    public void TransfersKeepRoutesAndReasonsSeparateAndExcludeExternalIncome()
    {
        var source = new Account { Name = "IRA" };
        var destination = new Account { Name = "Brokerage" };
        var year = new PlanYear();
        year.Transfers.AddRange([
            new("Move", source, destination, 100m, TransferReason.Scheduled),
            new("Move", source, destination, 25m, TransferReason.Scheduled),
            new("Move", destination, source, 50m, TransferReason.Scheduled),
            new("Move", source, null, 30m, TransferReason.Rmd),
            new("Move", source, null, 40m, TransferReason.ExpenseWithdrawal),
            new("Move", null, destination, 60m, TransferReason.SurplusReinvestment),
            new("Pension", null, null, 999m, TransferReason.ExternalIncome)
        ]);

        var columns = ForecastBreakdown.Transfers([year, new PlanYear()]);
        Assert.Equal(5, columns.Count);
        Assert.Equal(305m, columns.Sum(c => c.Value(year) ?? 0m));
        Assert.Contains(columns, c => c.Label.Contains("IRA → Brokerage") && c.Value(year) == 125m);
        Assert.Contains(columns, c => c.Label.Contains("Brokerage → IRA") && c.Value(year) == 50m);
        Assert.Contains(columns, c => c.Label.Contains("Cash pool → Brokerage"));
        Assert.Contains(columns, c => c.Label.Contains("Required minimum distribution"));
        Assert.All(columns, c => Assert.Equal(0m, c.Value(new PlanYear())));
        Assert.Empty(ForecastBreakdown.Transfers([]));
    }

    [Fact]
    public void ExpenseBreakdownExcludesTaxEvenWhenNamesMatch()
    {
        var year = new PlanYear { Expenses = [
            new() { Name = "Shared", Amount = 100m },
            new() { Name = "Shared", Amount = 20m },
            new() { Name = "Shared", Amount = 50m, Type = ExpenseType.Tax }] };
        var column = Assert.Single(ForecastBreakdown.Expenses([year]));
        Assert.Equal(120m, column.Value(year));
        Assert.Equal(0m, column.Value(new PlanYear()));
    }

    [Fact]
    public void MissingTaxCalculationIsUnavailableRatherThanZero()
    {
        var year = new PlanYear { Expenses = [new() { Type = ExpenseType.Tax, Amount = 500m }] };
        Assert.Equal(500m, year.TaxExpense);
        Assert.All(ForecastBreakdown.Taxes, column => Assert.Null(column.Value(year)));
    }

    [Fact]
    public void IncomeBreakdownReconcilesAndExcludesWithdrawals()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            FirstYearAnnualExpenses = 5000m,
            FirstYearTaxExpense = 100m,
            Accounts = [new Account { Name = "Brokerage", BeginningBalance = 100000m }],
            Income = [new Income { Name = "Pension", InitialAmount = 1000m, AgeStart = 58 }]
        };
        var years = new PlanCalculator().CalculateCurrentTrajectory(scenario).PlanYears;
        var columns = ForecastBreakdown.Income(years);
        Assert.Contains(columns, c => c.Label == "Pension");
        foreach (var year in years)
        {
            Assert.Equal(ForecastBreakdown.TotalIncome(year), columns.Sum(c => c.Value(year) ?? 0m));
            Assert.Equal(year.ExternalIncome + year.AccountEarnings.Sum(e => e.Amount), ForecastBreakdown.TotalIncome(year));
        }
        Assert.Contains(years[0].Transfers, t => t.Reason == TransferReason.ExpenseWithdrawal);
    }
}

