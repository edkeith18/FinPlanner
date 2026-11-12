using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class BasicValuesTest
{
    [Fact]
    public void PlansMatchExpectedValuesFromFinPlannerJson()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Input", "FinPlanner.json"));
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement.GetProperty("RegressionTest");
        var scenario = Scenario.Deserialize(json);
        var calculator = new PlanCalculator();

        var financial = calculator.CalculateCurrentTrajectory(scenario);
        var maximum = calculator.CalculateMaximumSpend(scenario);
        var perpetual = calculator.CalculatePerpetualSpend(scenario);

        Assert.True(financial.IsSuccessful, financial.FailureReason);
        Assert.True(maximum.IsSuccessful, maximum.FailureReason);
        Assert.True(perpetual.IsSuccessful, perpetual.FailureReason);

        Assert.Equal(expected.GetProperty("FinancialPlan").GetProperty("EndingBalance").GetDecimal(),
            financial.PlanYears[^1].EndingBalance);
        Assert.Equal(expected.GetProperty("MaximumSpendPlan").GetProperty("FirstYearTotalExpenses").GetDecimal(),
            maximum.PlanYears[0].TotalExpenses);
        Assert.Equal(expected.GetProperty("PerpetualSpendPlan").GetProperty("FirstYearTotalExpenses").GetDecimal(),
            perpetual.PlanYears[0].TotalExpenses);
    }
}
