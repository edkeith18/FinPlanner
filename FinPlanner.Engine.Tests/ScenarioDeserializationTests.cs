using System.Text;
using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ScenarioDeserializationTests
{
    [Theory]
    [InlineData(57, true, "Current age cannot be greater than life expectancy.")]
    [InlineData(100, false, "Scenario must contain at least one account.")]
    public async Task BothLoadersRejectInvalidScenarios(
        int lifeExpectancy, bool includeAccounts, string expectedMessage)
    {
        var json = CreateJson(lifeExpectancy, includeAccounts);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var syncError = Assert.Throws<ArgumentException>(() => Scenario.Deserialize(json));
        var asyncError = await Assert.ThrowsAsync<ArgumentException>(
            () => Scenario.DeserializeAsync(stream));

        Assert.StartsWith(expectedMessage, syncError.Message);
        Assert.Equal(syncError.Message, asyncError.Message);
        Assert.Equal("scenario", asyncError.ParamName);
    }

    [Fact]
    public async Task BothLoadersRejectJsonNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));

        var syncError = Assert.Throws<JsonException>(() => Scenario.Deserialize("null"));
        var asyncError = await Assert.ThrowsAsync<JsonException>(
            () => Scenario.DeserializeAsync(stream));

        Assert.Equal(syncError.Message, asyncError.Message);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(100)]
    public async Task BothLoadersNormalizeValidScenarios(int lifeExpectancy)
    {
        var json = CreateJson(lifeExpectancy, includeAccounts: true);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var syncScenario = Scenario.Deserialize(json);
        var asyncScenario = await Scenario.DeserializeAsync(stream);

        Assert.Equal(58, syncScenario.CurrentAge);
        Assert.Equal(58, asyncScenario.CurrentAge);
        Assert.Equal(1, syncScenario.Accounts[0].WithdrawalPriority);
        Assert.Equal(1, asyncScenario.Accounts[0].WithdrawalPriority);
        Assert.Equal(syncScenario.Serialize(), asyncScenario.Serialize());
    }

    private static string CreateJson(int lifeExpectancy, bool includeAccounts)
    {
        return JsonSerializer.Serialize(new
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58).ToString("MM/dd/yyyy"),
            LifeExpectancy = lifeExpectancy,
            Accounts = includeAccounts
                ? new[] { new { Id = Guid.NewGuid(), Name = "Savings", Balance = 1000m, WithdrawalPriority = 9 } }
                : []
        });
    }
}
