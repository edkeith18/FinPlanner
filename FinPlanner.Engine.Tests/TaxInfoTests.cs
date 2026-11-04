using System.Text.Json;
using System.Text.Json.Nodes;
using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class TaxInfoTests
{
    private static JsonObject Settings() => JsonNode.Parse(JsonSerializer.Serialize(TaxInfo.Default))!.AsObject();

    [Fact]
    public void BundledSettingsHaveExpectedUnitsAndTaxResults()
    {
        var info = TaxInfo.Default;
        Assert.Equal(0.85m, info.SocialSecurity.TaxablePercentage);
        Assert.Equal(0.0633m, info.StandardDeduction.AnnualRateOfIncrease);
        var year = new PlanYear { StartingYear = 2026, Transfers =
            [new AccountTransfer("Income", null, null, 100000m, TransferReason.ExternalIncome)] };
        Assert.Equal(7640m, TaxCalculator.Calculate(year, info).TotalTax);
    }

    [Theory]
    [InlineData(2025)]
    [InlineData(2026)]
    [InlineData(2028)]
    public void TaxGrowthUsesForecastOffsetRegardlessOfCalendarYear(int startingYear)
    {
        var first = new PlanYear { StartingYear = startingYear, YearsInFuture = 0 };
        var second = new PlanYear { StartingYear = startingYear + 1, YearsInFuture = 1 };
        Assert.Equal(32200m, TaxCalculator.Calculate(first, TaxInfo.Default).StandardDeduction);
        Assert.Equal(34238.26m, TaxCalculator.Calculate(second, TaxInfo.Default).StandardDeduction);
    }

    [Fact]
    public void ChangedSettingsControlDeductionsAndSocialSecurity()
    {
        var json = Settings();
        json["SocialSecurity"]!["TaxablePercentage"] = 50;
        json["StandardDeduction"]!["Amount"] = 40000;
        var info = TaxInfo.Deserialize(json.ToJsonString());
        var year = new PlanYear { StartingYear = 2026, Transfers =
            [new AccountTransfer("SS", null, null, 100000m, TransferReason.ExternalIncome)
                { TaxType = IncomeType.SocialSecurity }] };
        var result = TaxCalculator.Calculate(year, info);
        Assert.Equal(50000m, result.OrdinaryIncome);
        Assert.Equal(40000m, result.StandardDeduction);
        Assert.Equal(1000m, result.OrdinaryTax);
    }

    [Fact]
    public void SaltChangesInConfiguredCalendarYear()
    {
        var info = TaxInfo.Default;
        PlanYear Year(int year) => new() { StartingYear = year, Expenses =
            [new Expense { Type = ExpenseType.Salt, Amount = 50000m }] };
        Assert.Equal(50000m, TaxCalculator.Calculate(Year(2029), info).SaltDeduction);
        Assert.Equal(10000m, TaxCalculator.Calculate(Year(2030), info).SaltDeduction);
    }

    [Fact]
    public void PlanFundingUsesSuppliedSettingsThroughoutTrials()
    {
        var scenario = new Scenario
        {
            StartYear = 2026,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            FirstYearAnnualExpenses = 100000m,
            FirstYearTaxExpense = 7640m,
            Accounts = [new Account { Name = "IRA", Type = AccountType.Retirement, BeginningBalance = 1000000m }]
        };
        var json = Settings();
        json["StandardDeduction"]!["Amount"] = 200000;
        var original = new PlanCalculator().CalculateCurrentTrajectory(scenario);
        var calculator = new PlanCalculator();
        var changedScenario = new Scenario(scenario) { TaxInfo = TaxInfo.Deserialize(json.ToJsonString()) };
        var changed = calculator.CalculateCurrentTrajectory(changedScenario);
        var repeatedDefault = calculator.CalculateCurrentTrajectory(scenario);
        Assert.Equal(original.PlanYears[1].TaxExpense, repeatedDefault.PlanYears[1].TaxExpense);
        Assert.True(changed.IsSuccessful);
        Assert.Equal(7640m, changed.PlanYears[0].TaxExpense);
        Assert.Equal(0m, changed.PlanYears[1].TaxExpense);
        Assert.True(original.PlanYears[1].TaxExpense > changed.PlanYears[1].TaxExpense);
    }

    [Fact]
    public async Task ScenarioKeepsTaxSettingsInMemoryAndOutOfBothJsonWriters()
    {
        var settings = TaxInfo.Default with
        {
            StandardDeduction = TaxInfo.Default.StandardDeduction with { Amount = 200000m }
        };
        var scenario = new Scenario
        {
            TaxInfo = settings,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-58),
            LifeExpectancy = 60,
            Accounts = [new Account { Name = "IRA", Type = AccountType.Retirement }]
        };
        var copy = new Scenario(scenario);
        Assert.Same(settings, copy.TaxInfo);
        var json = scenario.Serialize();
        Assert.False(JsonNode.Parse(json)!.AsObject().ContainsKey("TaxInfo"));
        Assert.Same(TaxInfo.Default, Scenario.Deserialize(json).TaxInfo);
        using var stream = new MemoryStream();
        await scenario.SerializeAsync(stream);
        stream.Position = 0;
        var node = await JsonNode.ParseAsync(stream);
        Assert.False(node!.AsObject().ContainsKey("TaxInfo"));
    }

    [Fact]
    public void ScenarioRejectsInvalidTaxSettingsWithoutReplacingExistingSettings()
    {
        var scenario = new Scenario();
        var original = scenario.TaxInfo;
        Assert.Throws<JsonException>(() =>
            scenario.TaxInfo = original with { FilingStatus = "Single" });
        Assert.Same(original, scenario.TaxInfo);
        Assert.Throws<ArgumentNullException>(() => scenario.TaxInfo = null!);
        Assert.Same(original, scenario.TaxInfo);
    }

    [Fact]
    public void CapitalGainsGrowthIsIndependentOfOrdinaryBrackets()
    {
        var json = Settings();
        json["CapitalGainsTax"]!["ThresholdAnnualRateOfIncrease"] = 10;
        var year = new PlanYear { StartingYear = 2027, YearsInFuture = 1, Transfers =
            [new AccountTransfer("Gains", null, null, 150000m, TransferReason.ExternalIncome)
                { TaxType = IncomeType.Ltcg }] };
        var changed = TaxCalculator.Calculate(year, TaxInfo.Deserialize(json.ToJsonString()));
        Assert.Equal(6181.5m, changed.LTCGTax);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("negative")]
    [InlineData("percentage")]
    [InlineData("status")]
    [InlineData("brackets")]
    [InlineData("unknown")]
    public void InvalidSettingsAreRejected(string invalid)
    {
        var json = Settings();
        switch (invalid)
        {
            case "missing": json.Remove("StandardDeduction"); break;
            case "null": json["SocialSecurity"] = null; break;
            case "negative": json["StandardDeduction"]!["Amount"] = -1; break;
            case "percentage": json["SocialSecurity"]!["TaxablePercentage"] = 101; break;
            case "status": json["FilingStatus"] = "Single"; break;
            case "brackets": json["OrdinaryIncomeTax"]!["Brackets"]![1]!["Threshold"] = 0; break;
            case "unknown": json["Typo"] = 1; break;
        }
        Assert.Throws<JsonException>(() => TaxInfo.Deserialize(json.ToJsonString()));
    }

}
