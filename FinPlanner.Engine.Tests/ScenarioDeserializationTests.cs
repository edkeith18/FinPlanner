using System.Text;
using System.Text.Json;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class ScenarioDeserializationTests
{
    [Theory]
    [InlineData(57, true, "Life expectancy must be greater than current age.")]
    [InlineData(58, true, "Life expectancy must be greater than current age.")]
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
        Assert.Equal(includeAccounts ? null : "scenario", asyncError.ParamName);
    }

    [Theory]
    [InlineData(57)]
    [InlineData(58)]
    public async Task BothSerializersRejectLifeExpectancyAtOrBelowCurrentAge(int lifeExpectancy)
    {
        var scenario = new Scenario
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = lifeExpectancy
        };
        using var stream = new MemoryStream();

        var syncError = Assert.Throws<ArgumentException>(() => scenario.Serialize());
        var asyncError = await Assert.ThrowsAsync<ArgumentException>(() => scenario.SerializeAsync(stream));

        Assert.StartsWith("Life expectancy must be greater than current age.", syncError.Message);
        Assert.Equal(syncError.Message, asyncError.Message);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task BothSerializersPreserveValidScenario()
    {
        var scenario = Scenario.Deserialize(CreateJson(59, includeAccounts: true));
        using var stream = new MemoryStream();

        var json = scenario.Serialize();
        await scenario.SerializeAsync(stream);

        Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(59, Scenario.Deserialize(json).LifeExpectancy);
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
    [InlineData(59)]
    [InlineData(100)]
    public async Task BothLoadersPreserveValidScenarioPriorities(int lifeExpectancy)
    {
        var json = CreateJson(lifeExpectancy, includeAccounts: true);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var syncScenario = Scenario.Deserialize(json);
        var asyncScenario = await Scenario.DeserializeAsync(stream);

        Assert.Equal(58, syncScenario.CurrentAge);
        Assert.Equal(58, asyncScenario.CurrentAge);
        Assert.Equal(9, syncScenario.Accounts[0].WithdrawalPriority);
        Assert.Equal(9, asyncScenario.Accounts[0].WithdrawalPriority);
        Assert.Equal(syncScenario.Serialize(), asyncScenario.Serialize());
    }

    [Theory]
    [InlineData("\"FirstYearAnnualExpenses\": -1")]
    [InlineData("\"FirstYearTaxExpense\": -0.01")]
    [InlineData("\"AnnualInflationRate\": -2.5")]
    [InlineData("\"Income\": [{\"InitialAmount\": -100}]")]
    [InlineData("\"Accounts\": [{\"WithdrawalPriority\": -1}]")]
    [InlineData("\"Expenses\": [{\"AnnualRateOfIncrease\": -0.5}]")]
    [InlineData("\"Transfers\": [{\"AgeEnd\": -1}]")]
    [InlineData("\"Unknown\": {\"Nested\": [0, {\"Number\": -1e2}]}")]
    public async Task BothLoadersRejectNegativeNumbersAnywhere(string property)
    {
        var json = "{" + property + "," + CreateJson(100, includeAccounts: true)[1..];
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var syncError = Assert.Throws<JsonException>(() => Scenario.Deserialize(json));
        var asyncError = await Assert.ThrowsAsync<JsonException>(() => Scenario.DeserializeAsync(stream));

        Assert.Equal("JSON numbers must be non-negative.", syncError.Message);
        Assert.Equal(syncError.Message, asyncError.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.125")]
    [InlineData("42")]
    [InlineData("1e2")]
    [InlineData("-0")]
    public async Task BothLoadersAcceptNonNegativeNumbersAndIgnoreNumericStrings(string number)
    {
        var json = "{\"Unknown\": {\"Nested\": [" + number +
            ", \"-100\"]}," + CreateJson(100, includeAccounts: true)[1..];
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var syncScenario = Scenario.Deserialize(json);
        var asyncScenario = await Scenario.DeserializeAsync(stream);

        Assert.Equal(syncScenario.Serialize(), asyncScenario.Serialize());
    }

    [Fact]
    public async Task StreamLoaderAcceptsUtf8ByteOrderMark()
    {
        var json = CreateJson(100, includeAccounts: true);
        using var stream = new MemoryStream(
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray());

        var scenario = await Scenario.DeserializeAsync(stream);

        Assert.Single(scenario.Accounts);
        Assert.True(stream.CanRead);
    }

    private static string CreateJson(int lifeExpectancy, bool includeAccounts)
    {
        return JsonSerializer.Serialize(new
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58).ToString("MM/dd/yyyy"),
            LifeExpectancy = lifeExpectancy,
            Accounts = includeAccounts
                ? new[] { new { Id = Guid.NewGuid(), Name = "Savings", Balance = 1000m, Basis = 0m, WithdrawalPriority = 9 } }
                : []
        });
    }
}
