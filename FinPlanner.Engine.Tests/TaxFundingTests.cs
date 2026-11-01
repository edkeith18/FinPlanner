using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class TaxFundingTests
{
    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
        LifeExpectancy = 60,
        FirstYearAnnualExpenses = 100000m,
        FirstYearTaxExpense = 7640m,
        Accounts = [new Account { Name = "IRA", Type = AccountType.Retirement, BeginningBalance = 1000000m }]
    };

    [Fact]
    public void SuppliedTaxExpenseFitsFirstYearTaxesInsideTheAnnualBudget()
    {
        var scenario = CreateScenario();
        var original = scenario.Serialize();
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.True(plan.IsSuccessful);
        var first = plan.PlanYears[0];
        Assert.Equal(100000m, first.TotalExpenses);
        Assert.Equal(7640m, first.TaxExpense);
        Assert.Null(first.Taxes);
        Assert.Equal(92360m, first.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
        Assert.Equal(900000m, first.EndingBalance);
        Assert.Equal(original, scenario.Serialize());
    }

    [Fact]
    public void LaterYearsGrossUpTaxableWithdrawalsAndCommitOnlyTheFinalTrial()
    {
        var plan = new PlanCalculator().CalculateCurrentTrajectory(CreateScenario());
        var second = plan.PlanYears[1];
        Assert.Equal(92360m, second.Expenses.Single(e => e.Type == ExpenseType.Uncategorized).Amount);
        Assert.True(second.TaxExpense > 0m);
        Assert.Equal(second.TotalExpenses, second.TotalCashFlow);
        Assert.Equal(second.Accounts[0].BeginningBalance - second.TotalExpenses, second.EndingBalance);
        Assert.Single(second.Transfers, t => t.Reason == TransferReason.ExpenseWithdrawal);
        Assert.Equal(decimal.Round(second.Taxes!.TotalTax, 0, MidpointRounding.AwayFromZero), second.TaxExpense);
    }

    [Fact]
    public void NamedExpensesAndTaxesCanExceedTheFirstYearBudgetEvenWithEnoughAssets()
    {
        var scenario = CreateScenario();
        scenario.Expenses.Add(new Expense { Amount = 99000m, AgeStart = 58, AgeEnd = 59 });
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Empty(plan.PlanYears);
        Assert.Contains("First-year tax and named expenses", plan.FailureReason);
    }

    [Fact]
    public void TaxesAreRecalculatedFromActualCappedWithdrawalsWhenFundsRunOut()
    {
        var scenario = CreateScenario();
        scenario.LifeExpectancy = 62;
        scenario.Accounts[0].BeginningBalance = 150000m;
        var plan = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        Assert.False(plan.IsSuccessful);
        Assert.Equal(0m, plan.PlanYears[^1].EndingBalance);
        Assert.True(plan.PlanYears[^1].UnfundedExpenses > 0m);
    }

    [Fact]
    public void ExternalSocialSecurityClassificationSurvivesScenarioCopyAndJson()
    {
        var scenario = CreateScenario();
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        scenario.Income.Add(new Income { Name = "Social Security", InitialAmount = 100000m, AgeStart = 58 });
        var copy = Scenario.Deserialize(scenario.Serialize());
        var plan = new PlanCalculator().CalculateCurrentTrajectory(copy);
        Assert.Equal(7640m, plan.PlanYears[0].TaxExpense);
        Assert.Null(plan.PlanYears[0].Taxes);
        Assert.Equal(85000m, plan.PlanYears[1].Taxes!.OrdinaryIncome);
        Assert.Equal(100000m, plan.PlanYears[1].ExternalIncome);
    }

    [Fact]
    public void MaximumSpendIncludesTaxesInEveryReturnedYear()
    {
        var scenario = CreateScenario();
        scenario.Accounts.Add(new Account { Type = AccountType.Brokerage });
        var plan = new PlanCalculator().CalculateMaximumSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.Null(plan.PlanYears[0].Taxes);
        Assert.Equal(scenario.FirstYearTaxExpense, plan.PlanYears[0].TaxExpense);
        Assert.All(plan.PlanYears.Skip(1), year => Assert.NotNull(year.Taxes));
    }

    [Fact]
    public void ExplicitTaxClassificationsRoundTripAndCopy()
    {
        var scenario = CreateScenario();
        scenario.Income.Add(new Income { Name = "Other", InitialAmount = 100m,
            Type = IncomeType.Interest });
        scenario.Expenses.Add(new Expense { Name = "Donation", Type = ExpenseType.Charity, Amount = 100m });
        var copy = new Scenario(Scenario.Deserialize(scenario.Serialize()));
        Assert.Equal(IncomeType.Interest, copy.Income[0].Type);
        Assert.Equal(ExpenseType.Charity, copy.Expenses[0].Type);
    }

    [Fact]
    public void TaxAwarePerpetualBudgetsAreCertifiedAndClearlyMarkedAsConservative()
    {
        var scenario = CreateScenario();
        scenario.SecuritiesAnnualRateOfReturn = 0.07m;
        scenario.Accounts[0].Type = AccountType.Brokerage;
        var plan = new PlanCalculator().CalculatePerpetualSpend(scenario);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.AnnualExpenses > 0m);
        Assert.Contains("exact maximum is not proven", plan.CalculationNote);
        Assert.Null(plan.PlanYears[0].Taxes);
        Assert.Equal(scenario.FirstYearTaxExpense, plan.PlanYears[0].TaxExpense);
        Assert.All(plan.PlanYears.Skip(1), year => Assert.NotNull(year.Taxes));
    }
}
