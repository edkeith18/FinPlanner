using FinPlanner.Engine;
using FinPlanner.Web.Services;

namespace FinPlanner.Web.Tests;

public class SpendingSummaryTests
{
    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
        LifeExpectancy = 60,
        FirstYearAnnualExpenses = 1000m,
        Accounts = [new Account { Name = "Savings", BeginningBalance = 100000m }]
    };

    [Fact]
    public void ScenarioNotificationPreparesAllPlansAndSidebarValues()
    {
        var scenario = CreateScenario();
        using var results = new SpendingSummary(scenario, new PlanCalculator());
        Assert.Null(results.FinancialPlan);

        scenario.NotifyStateChanged();

        Assert.NotNull(results.FinancialPlan);
        Assert.True(results.MaximumPlan!.IsSuccessful);
        Assert.True(results.PerpetualPlan!.IsSuccessful);
        Assert.Equal(results.MaximumPlan.PlanYears[0].TotalExpenses, results.MaximumSpend);
        Assert.Equal(results.PerpetualPlan.PlanYears[0].TotalExpenses, results.PerpetualSpend);
    }

    [Fact]
    public void UnchangedInputsReusePlansButEditsRefreshThem()
    {
        var scenario = CreateScenario();
        using var results = new SpendingSummary(scenario, new PlanCalculator());
        scenario.NotifyStateChanged();
        var original = results.FinancialPlan;
        var notifications = 0;
        results.Changed += () => notifications++;

        scenario.NotifyStateChanged();
        results.Calculate();
        Assert.Same(original, results.FinancialPlan);
        Assert.Equal(0, notifications);

        scenario.FirstYearAnnualExpenses = 2000m;
        scenario.NotifyStateChanged();
        Assert.NotSame(original, results.FinancialPlan);
        Assert.Equal(2000m, results.FinancialPlan!.PlanYears[0].TotalExpenses);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void InvalidReplacementClearsOldPlansAndCanRecover()
    {
        var scenario = CreateScenario();
        using var results = new SpendingSummary(scenario, new PlanCalculator());
        scenario.NotifyStateChanged();
        var accounts = scenario.Accounts;
        scenario.Accounts = [];
        scenario.NotifyStateChanged();

        Assert.Null(results.FinancialPlan);
        Assert.Null(results.MaximumSpend);
        Assert.Null(results.PerpetualSpend);
        Assert.NotNull(results.FinancialError);
        Assert.NotNull(results.MaximumError);
        Assert.NotNull(results.PerpetualError);

        scenario.Accounts = accounts;
        scenario.NotifyStateChanged();
        Assert.NotNull(results.FinancialPlan);
        Assert.Null(results.FinancialError);
    }
}
