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
        var uncategorized = scenario.FirstYearAnnualExpenses - scenario.FirstYearTaxExpense.GetValueOrDefault() - 20000m;
        var baselineExpenses = PlanYearCalculator.CreateExpenses(scenario, uncategorized);
        var first = PlanYearCalculator.FromScenario(scenario, baselineExpenses);
        Assert.Equal(12000m, first.TaxExpense);
        Assert.Equal(0, first.YearsInFuture);

        scenario.FirstYearTaxExpense = 30000m;
        PlanYearCalculator.Calculate(first, TaxInfo.Default);

        Assert.Equal(12000m, first.TaxExpense);
        Assert.Null(first.Taxes);
        Assert.Equal(100000m, first.TotalExpenses);

        var second = PlanYearCalculator.ForecastFrom(first, scenario, baselineExpenses);
        Assert.Equal(0m, second.TaxExpense);
        Assert.Equal(1, second.YearsInFuture);
        PlanYearCalculator.Calculate(second, TaxInfo.Default);
        Assert.NotNull(second.Taxes);
        Assert.Equal(decimal.Round(second.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), second.TaxExpense);
        var third = PlanYearCalculator.ForecastFrom(second, scenario, baselineExpenses);
        Assert.Equal(2, third.YearsInFuture);
        Assert.Equal(first.StartingYear + 2, third.StartingYear);
    }

    [Fact]
    public void BaselineExpensesAreIndependentOfScenarioAndYearCopies()
    {
        var scenario = CreateScenario();
        scenario.AnnualInflationRate = 2m;
        var baselineExpenses = PlanYearCalculator.CreateExpenses(scenario, 68000m);
        var first = PlanYearCalculator.FromScenario(scenario, baselineExpenses);

        scenario.Expenses[0].Amount = 1m;
        first.Expenses.Single(expense => expense.Name == "Living").Amount = 2m;
        // A finalized first-year budget can update the baseline without changing past years.
        baselineExpenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount = 60000m;
        var second = PlanYearCalculator.ForecastFrom(first, scenario, baselineExpenses);
        var third = PlanYearCalculator.ForecastFrom(second, scenario, baselineExpenses);

        Assert.Equal(20000m, second.Expenses.Single(expense => expense.Name == "Living").Amount);
        Assert.Equal(68000m, first.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
        Assert.Equal(61200m, second.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
        Assert.Equal(62424m, third.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
        Assert.Single(scenario.Expenses);
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
    public void NullFirstYearTaxCalculatesTaxWithinTheCurrentTrajectoryBudget()
    {
        var scenario = CreateScenario();
        scenario.FirstYearTaxExpense = null;
        var plan = new PlanCalculator().CalculateCurrentTrajectory(Scenario.Deserialize(scenario.Serialize()));
        var first = plan.PlanYears[0];

        Assert.True(plan.IsSuccessful);
        Assert.NotNull(first.Taxes);
        Assert.True(first.TaxExpense > 0m);
        Assert.Equal(100000m, first.TotalExpenses);
        Assert.Equal(80000m - first.TaxExpense,
            first.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
    }

    [Fact]
    public void CurrentTrajectoryPreservesFirstYearBudgetExpenseAndTaxCents()
    {
        var scenario = CreateScenario();
        scenario.FirstYearAnnualExpenses = 100000.37m;
        scenario.FirstYearTaxExpense = 12000.23m;
        scenario.Expenses[0].Amount = 20000.41m;
        var original = scenario.Serialize();

        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        var first = plan.PlanYears[0];

        Assert.True(plan.IsSuccessful);
        Assert.Equal(100000.37m, first.TotalExpenses);
        Assert.Equal(12000.23m, first.TaxExpense);
        Assert.Equal(20000.41m, first.Expenses.Single(expense => expense.Name == "Living").Amount);
        Assert.Equal(67999.73m, first.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
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
    public void SpendingSearchesCalculateTaxesAndCoverNamedExpenses()
    {
        var scenario = CreateScenario();
        scenario.Accounts = [new Account { Type = AccountType.Brokerage, BeginningBalance = 10000000m }];
        scenario.SecuritiesAnnualRateOfReturn = 0.07m;
        var original = scenario.Serialize();
        var calculator = new PlanCalculator();
        foreach (var plan in new[] { calculator.CalculateMaximumSpend(scenario), calculator.CalculatePerpetualSpend(scenario) })
        {
            Assert.True(plan.IsSuccessful);
            var first = plan.PlanYears[0];
            Assert.NotNull(first.Taxes);
            Assert.Equal(decimal.Round(first.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), first.TaxExpense);
            Assert.Equal(plan.AnnualExpenses, first.TotalExpenses);
            Assert.True(first.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount >= 0m);
            Assert.Equal(first.TotalExpenses - first.TaxExpense - 20000m,
                first.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
            Assert.NotNull(plan.PlanYears[1].Taxes);
        }
        Assert.Equal(original, scenario.Serialize());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchBudgetsAndTaxesDoNotDependOnSuppliedActualTax(bool perpetual)
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var calculator = new PlanCalculator();
        Plan Search() => perpetual ? calculator.CalculatePerpetualSpend(scenario) : calculator.CalculateMaximumSpend(scenario);
        var expected = Search();
        Assert.True(expected.IsSuccessful);
        // An actual tax larger than any feasible budget must not set the search's lower bound.
        scenario.FirstYearTaxExpense = 10000000m;
        var original = scenario.Serialize();
        var actual = Search();
        Assert.True(actual.IsSuccessful);
        Assert.Equal(expected.AnnualExpenses, actual.AnnualExpenses);
        Assert.Equal(expected.PlanYears[0].TaxExpense, actual.PlanYears[0].TaxExpense);
        Assert.True(actual.PlanYears[0].TaxExpense > 0m);
        Assert.Equal(original, scenario.Serialize());
        Assert.Equal(expected.AnnualExpenses, Search().AnnualExpenses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstYearSearchTaxUsesTheFundedCandidateWithdrawals(bool perpetual)
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var calculator = new PlanCalculator();
        var plan = perpetual ? calculator.CalculatePerpetualSpend(scenario) : calculator.CalculateMaximumSpend(scenario);
        Assert.True(plan.IsSuccessful);
        var first = plan.PlanYears[0];
        // All first-year cash comes from a traditional IRA in this scenario.
        Assert.Equal(plan.AnnualExpenses, first.TotalCashFlow);
        Assert.Equal(plan.AnnualExpenses, first.Taxes!.OrdinaryIncome);
        Assert.Equal(TaxCalculator.Calculate(first, scenario.TaxInfo).TotalTax, first.Taxes.TotalTax);
        Assert.Equal(decimal.Round(first.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), first.TaxExpense);
        var discretionary = first.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount;
        Assert.Equal(discretionary, plan.PlanYears[1].Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchPreservesNamedExpenseCentsAndReclassifiesTaxesWithinBudget(bool perpetual)
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.1m;
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        scenario.Expenses[0].Amount = 20000.4m;
        scenario.Expenses.Add(new Expense { Name = "Other", Amount = 1000.4m, AgeStart = 58, AgeEnd = 60 });
        var calculator = new PlanCalculator();
        var plan = perpetual ? calculator.CalculatePerpetualSpend(scenario) : calculator.CalculateMaximumSpend(scenario);
        Assert.True(plan.IsSuccessful);
        var first = plan.PlanYears[0];
        Assert.Equal(plan.AnnualExpenses, first.TotalExpenses);
        Assert.Equal(plan.AnnualExpenses, first.TotalCashFlow);
        Assert.Equal(plan.AnnualExpenses, first.Taxes!.OrdinaryIncome);
        Assert.Equal(20000.4m, first.Expenses.Single(expense => expense.Name == "Living").Amount);
        Assert.Equal(1000.4m, first.Expenses.Single(expense => expense.Name == "Other").Amount);
        Assert.Equal(plan.AnnualExpenses - 21000.8m - first.TaxExpense,
            first.Expenses.Single(expense => expense.Type == ExpenseType.Uncategorized).Amount);
        Assert.Equal(decimal.Round(first.Taxes.TotalTax, 0, MidpointRounding.AwayFromZero), first.TaxExpense);
    }
}
