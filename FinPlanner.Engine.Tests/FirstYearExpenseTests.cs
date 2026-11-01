using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class FirstYearExpenseTests
{
    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
        LifeExpectancy = 60,
        FirstYearAnnualExpenses = 100000m,
        FirstYearTaxExpense = 12000m,
        Accounts = [new Account { Name = "IRA", Type = AccountType.Retirement, BeginningBalance = 1000000m }],
        Expenses = [new Expense { Name = "Living", Amount = 20000m, AgeStart = 58, AgeEnd = 60 }]
    };

    [Fact]
    public void FirstYearCopiesTaxExpenseBeforeCalculationAndPreservesItAfterScenarioEdits()
    {
        var scenario = CreateScenario();
        var uncategorized = scenario.FirstYearAnnualExpenses - scenario.FirstYearTaxExpense - 20000m;
        var first = PlanYearCalculator.FromScenario(scenario, uncategorized);
        Assert.Equal(12000m, first.TaxExpense);
        Assert.Equal(0, first.YearsInFuture);

        scenario.FirstYearTaxExpense = 30000m;
        PlanYearCalculator.Calculate(first);

        Assert.Equal(12000m, first.TaxExpense);
        Assert.Null(first.Taxes);
        Assert.Equal(100000m, first.TotalExpenses);

        var second = PlanYearCalculator.ForecastFrom(first, scenario, uncategorized);
        Assert.Equal(0m, second.TaxExpense);
        Assert.Equal(1, second.YearsInFuture);
        PlanYearCalculator.Calculate(second);
        Assert.NotNull(second.Taxes);
        Assert.Equal(decimal.Round(second.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), second.TaxExpense);
        var third = PlanYearCalculator.ForecastFrom(second, scenario, uncategorized);
        Assert.Equal(2, third.YearsInFuture);
        Assert.Equal(first.StartingYear + 2, third.StartingYear);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12000)]
    [InlineData(36958)]
    public void SuppliedTaxesDetermineFirstYearSpendingAndLaterTaxesAreCalculated(int suppliedTax)
    {
        var scenario = CreateScenario();
        scenario.FirstYearTaxExpense = suppliedTax;
        scenario.AnnualInflationRate = 2m;
        scenario.Expenses.Add(new Expense { Name = "Future", Amount = 1000m, AgeStart = 59, AgeEnd = 59 });
        scenario.Expenses.Add(new Expense { Name = "Expired", Amount = 1000m, AgeStart = 50, AgeEnd = 57 });
        var original = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        var first = plan.PlanYears[0];
        var second = plan.PlanYears[1];
        var uncategorized = 80000m - suppliedTax;
        Assert.Equal(suppliedTax, first.TaxExpense);
        Assert.Null(first.Taxes);
        Assert.Equal(100000m, first.TotalExpenses);
        Assert.Equal(first.TotalExpenses, first.AnnualExpenses);
        Assert.Equal(900000m, first.EndingBalance);
        Assert.Equal(uncategorized, first.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
        Assert.Equal(decimal.Round(uncategorized * 1.02m, 0, MidpointRounding.AwayFromZero),
            second.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
        Assert.NotNull(second.Taxes);
        Assert.Equal(decimal.Round(second.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), second.TaxExpense);
        Assert.Equal(second.TotalExpenses, second.AnnualExpenses);
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public async Task FirstYearInputsRoundTripThroughBothJsonReadersAndScenarioCopies()
    {
        var scenario = CreateScenario();
        var json = scenario.Serialize();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(100000m, document.RootElement.GetProperty("FirstYearAnnualExpenses").GetDecimal());
        Assert.Equal(12000m, document.RootElement.GetProperty("FirstYearTaxExpense").GetDecimal());
        Assert.False(document.RootElement.TryGetProperty("AnnualExpenses", out _));
        using var stream = new MemoryStream();
        await scenario.SerializeAsync(stream);
        stream.Position = 0;
        foreach (var copy in new[] { new Scenario(scenario), Scenario.Deserialize(json), await Scenario.DeserializeAsync(stream) })
        {
            Assert.Equal(scenario.FirstYearAnnualExpenses, copy.FirstYearAnnualExpenses);
            Assert.Equal(scenario.FirstYearTaxExpense, copy.FirstYearTaxExpense);
        }
    }

    [Fact]
    public void SuppliedFirstYearTaxIsStillFundedWhenAssetsRunOut()
    {
        var scenario = CreateScenario();
        scenario.Accounts[0].BeginningBalance = 50000m;
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        var year = Assert.Single(plan.PlanYears);
        Assert.Equal(12000m, year.TaxExpense);
        Assert.Null(year.Taxes);
        Assert.Equal(50000m, year.UnfundedExpenses);
        Assert.Equal(0m, year.EndingBalance);
    }

    [Fact]
    public void SpendingSearchesKeepSuppliedTaxesFixedAndCoverNamedExpenses()
    {
        var scenario = CreateScenario();
        scenario.Accounts = [new Account { Type = AccountType.Brokerage, BeginningBalance = 10000000m }];
        scenario.SecuritiesAnnualRateOfReturn = 0.07m;
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        foreach (var plan in new[] { calculator.CalculateMaximumSpend(scenario), calculator.CalculatePerpetualSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            Assert.True(plan.AnnualExpenses >= 32000m);
            Assert.Equal(12000m, plan.PlanYears[0].TaxExpense);
            Assert.Null(plan.PlanYears[0].Taxes);
            Assert.Equal(plan.AnnualExpenses, plan.PlanYears[0].TotalExpenses);
            Assert.NotNull(plan.PlanYears[1].Taxes);
        }
        Assert.Equal(original, scenario.Serialize());
    }
}
