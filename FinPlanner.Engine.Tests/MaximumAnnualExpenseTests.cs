using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class MaximumAnnualExpenseTests
{
    [Fact]
    public void MaximumSpendCanExhaustAssetsInTheFinalYearWithoutExternalIncome()
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }]
        };
        var calculator = new PlanCalculator();

        var plan = calculator.CalculateMaximumSpend(scenario);

        Assert.True(plan.IsSuccessful);
        Assert.Equal(5000m, plan.AnnualExpenses);
        Assert.Equal(2, plan.PlanYears.Count);
        Assert.All(plan.PlanYears, year => Assert.True(year.IsFullyFunded));
        Assert.Equal(0m, plan.PlanYears[^1].EndingBalance);
        Assert.Equal(0m, plan.PlanYears[^1].ExternalIncome);

        scenario.FirstYearAnnualExpenses = plan.AnnualExpenses + 1000m;
        Assert.False(calculator.CalculateCurrentTrajectory(scenario).IsSuccessful);
    }

    [Theory]
    [InlineData(2, 0, 0, 0, 0, 5000)]
    [InlineData(2, 0, 0, 1000, 1, 4000)]
    [InlineData(2, 0, 0.1, 0, 0, 4000)]
    [InlineData(1, 0.2, 0, 0, 0, 12000)]
    [InlineData(2, 0, 0, 10000, 0, 0)]
    public void SearchUsesBuildAndPreservesOriginalInputsAndResults(
        int periods, double growth, double inflation, int namedExpense,
        double expenseGrowth, int expected)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 58 + periods,
            FirstYearAnnualExpenses = 10000m,
            SecuritiesAnnualRateOfReturn = (decimal)growth,
            AnnualInflationRate = (decimal)inflation * 100m,
            Accounts = [new Account { Type = AccountType.Hsa, BeginningBalance = 10000m }],
            Expenses = [new Expense
            {
                Name = "Named expense", Amount = namedExpense,
                AgeStart = 58, AgeEnd = 100,
                AnnualRateOfIncrease = (decimal)expenseGrowth
            }]
        };
        var originalJson = scenario.Serialize();
        var calculator = new PlanCalculator();
        var plan = calculator.CalculateCurrentTrajectory(scenario);
        Assert.Equal("Financial Plan", plan.Name);
        var originalBalances = plan.PlanYears.Select(year => year.EndingBalance).ToArray();

        var maximumSpendPlan = calculator.CalculateMaximumSpend(scenario);
        if (expected > 0)
        {
            Assert.Equal((decimal)expected, maximumSpendPlan.AnnualExpenses);
            Assert.Equal("Maximum Spend Plan", maximumSpendPlan.Name);
            Assert.True(maximumSpendPlan.IsSuccessful);
            Assert.Equal((decimal)expected, maximumSpendPlan.PlanYears[0].AnnualExpenses);
            Assert.Equal(scenario.LifeExpectancy, maximumSpendPlan.PlanYears[^1].EndingAge);
        }
        else
        {
            Assert.False(maximumSpendPlan.IsSuccessful);
            Assert.Empty(maximumSpendPlan.PlanYears);
            Assert.Equal("No sustainable spending plan was found.", maximumSpendPlan.FailureReason);
        }
        var repeatedPlan = calculator.CalculateMaximumSpend(scenario);
        Assert.Equal(maximumSpendPlan.IsSuccessful, repeatedPlan.IsSuccessful);
        Assert.Equal(maximumSpendPlan.AnnualExpenses, repeatedPlan.AnnualExpenses);
        Assert.Equal(originalJson, scenario.Serialize());
        Assert.Equal(originalBalances, plan.PlanYears.Select(year => year.EndingBalance).ToArray());
    }
}
