using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class MaximumAnnualExpenseTests
{
    [Theory]
    [InlineData(2, 0, 0, 0, 0, 4000)]
    [InlineData(2, 0, 0, 1000, 1, 4000)]
    [InlineData(2, 0, 0.1, 0, 0, 4000)]
    [InlineData(1, 0.2, 0, 0, 0, 11000)]
    [InlineData(2, 0, 0, 10000, 0, 0)]
    public void SearchUsesBuildAndPreservesOriginalInputsAndResults(
        int periods, double growth, double inflation, int namedExpense,
        double expenseGrowth, int expected)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 58 + periods,
            AnnualExpenses = 10000m,
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
        Assert.Equal((decimal)expected, maximumSpendPlan.AnnualExpenses);
        if (expected > 0)
        {
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
        Assert.Equal((decimal)expected, calculator.CalculateMaximumSpend(scenario).AnnualExpenses);
        Assert.Equal(originalJson, scenario.Serialize());
        Assert.Equal(originalBalances, plan.PlanYears.Select(year => year.EndingBalance).ToArray());
    }
}
