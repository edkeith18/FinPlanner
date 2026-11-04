using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class WithdrawalPriorityTests
{
    private static Scenario CreateScenario() => new()
    {
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
        LifeExpectancy = 60,
        FirstYearAnnualExpenses = 10000m,
        SecuritiesAnnualRateOfReturn = 0.1m,
        Accounts =
        [
            new Account { Name = "Later", Type = AccountType.Hsa, BeginningBalance = 100000m, WithdrawalPriority = 9 },
            new Account { Name = "First", Type = AccountType.Hsa, BeginningBalance = 100000m, WithdrawalPriority = 1 }
        ]
    };

    [Fact]
    public async Task BothJsonReadersAndWritersPreservePrioritiesIndependentOfArrayOrder()
    {
        var scenario = CreateScenario();
        var json = scenario.Serialize();
        using var stream = new MemoryStream();
        await scenario.SerializeAsync(stream);
        stream.Position = 0;
        var asyncCopy = await Scenario.DeserializeAsync(stream);
        foreach (var copy in new[] { scenario, Scenario.Deserialize(json), asyncCopy })
        {
            Assert.Equal(new[] { "Later", "First" }, copy.Accounts.Select(a => a.Name));
            Assert.Equal(new[] { 9, 1 }, copy.Accounts.Select(a => a.WithdrawalPriority));
        }
    }

    [Theory]
    [InlineData("current")]
    [InlineData("maximum")]
    [InlineData("perpetual")]
    public void AllPlanModesWithdrawByPersistedPriorityInsteadOfArrayOrder(string mode)
    {
        var scenario = Scenario.Deserialize(CreateScenario().Serialize());
        var calculator = new PlanCalculator();
        var plan = mode switch
        {
            "current" => calculator.CalculateCurrentTrajectory(scenario),
            "maximum" => calculator.CalculateMaximumSpend(scenario),
            _ => calculator.CalculatePerpetualSpend(scenario)
        };
        Assert.True(plan.IsSuccessful);
        var withdrawal = plan.PlanYears[0].Transfers.First(t => t.Reason == TransferReason.ExpenseWithdrawal);
        Assert.Equal("First", withdrawal.FromAccount!.Name);
        Assert.Equal(new[] { 9, 1 }, scenario.Accounts.Select(a => a.WithdrawalPriority));
    }

    [Fact]
    public void AddingAnAccountAppendsItsPriorityWithoutRenumberingExistingAccounts()
    {
        var scenario = CreateScenario();
        scenario.AddAccount("New", 100m);
        Assert.Equal(new[] { 9, 1, 10 }, scenario.Accounts.Select(a => a.WithdrawalPriority));
    }
}
