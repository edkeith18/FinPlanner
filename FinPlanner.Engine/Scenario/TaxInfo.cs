using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

/// <summary>Validated tax parameters. JSON percentages are stored as fractional rates.</summary>
public sealed record TaxInfo
{
    public required int TaxYear { get; init; }
    public required string FilingStatus { get; init; }
    public required SocialSecuritySettings SocialSecurity { get; init; }
    public required StandardDeductionSettings StandardDeduction { get; init; }
    public required HealthcareDeductionSettings HealthcareDeduction { get; init; }
    public required CharityDeductionSettings CharityDeduction { get; init; }
    public required SaltDeductionSettings SaltDeduction { get; init; }
    public required OrdinaryIncomeTaxSettings OrdinaryIncomeTax { get; init; }
    public required CapitalGainsTaxSettings CapitalGainsTax { get; init; }
    public required NetInvestmentIncomeTaxSettings NetInvestmentIncomeTax { get; init; }
    public required AlternativeMinimumTaxSettings AlternativeMinimumTax { get; init; }

    private static readonly Lazy<TaxInfo> DefaultValue = new(() =>
    {
        using var stream = typeof(TaxInfo).Assembly.GetManifestResourceStream("FinPlanner.TaxInfo.json")
            ?? throw new InvalidOperationException("Bundled tax settings are missing.");
        using var reader = new StreamReader(stream);
        return Deserialize(reader.ReadToEnd());
    });
    public static TaxInfo Default => DefaultValue.Value;

    /// <summary>Reads tax settings from a JSON file and validates them.</summary>
    public static TaxInfo Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>Deserializes tax settings, rejects unknown fields, and validates the resulting values.</summary>
    public static TaxInfo Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<TaxInfo>(json, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new JsonException("Tax settings cannot be null.");
        settings.Validate();
        return settings;
    }

    /// <summary>Validates the tax year, filing status, parameter ranges, brackets, and SALT change year.</summary>
    public void Validate()
    {
        if (TaxYear < 1 || TaxYear > 9999 || FilingStatus != "MarriedFilingJointly")
            throw new JsonException("Tax settings require a valid base year and MarriedFilingJointly status.");
        ValidateObject(this);
        ValidateBrackets(OrdinaryIncomeTax.Brackets);
        ValidateBrackets(CapitalGainsTax.Brackets);
        if (SaltDeduction.LimitChangeYear < TaxYear || SaltDeduction.LimitChangeYear > 9999)
            throw new JsonException("SALT change year must be at or after the tax base year.");
    }

    /// <summary>Recursively checks required tax settings, numeric ranges, and bracket entries.</summary>
    private static void ValidateObject(object value)
    {
        foreach (var property in value.GetType().GetProperties().Where(p => p.Name != nameof(Default)))
        {
            var item = property.GetValue(value) ?? throw new JsonException($"{property.Name} is required.");
            if (item is decimal number)
            {
                if (number < 0m || (property.IsDefined(typeof(JsonConverterAttribute), false) && number > 1m))
                    throw new JsonException($"{property.Name} is outside its allowed range.");
            }
            else if (item is ImmutableArray<Bracket> brackets)
            {
                if (brackets.IsDefaultOrEmpty) throw new JsonException("Tax brackets are required.");
                foreach (var bracket in brackets)
                    ValidateObject(bracket ?? throw new JsonException("Tax brackets cannot be null."));
            }
            else if (item.GetType().IsClass && item is not string) ValidateObject(item);
        }
    }

    /// <summary>Requires brackets to start at zero with strictly increasing thresholds and nondecreasing rates.</summary>
    private static void ValidateBrackets(ImmutableArray<Bracket> brackets)
    {
        if (brackets.IsDefaultOrEmpty || brackets[0].Threshold != 0m)
            throw new JsonException("Brackets must start at zero.");
        for (var i = 1; i < brackets.Length; i++)
            if (brackets[i].Threshold <= brackets[i - 1].Threshold || brackets[i].Rate < brackets[i - 1].Rate)
                throw new JsonException("Bracket thresholds must increase strictly and rates must not decrease.");
    }

    public sealed record SocialSecuritySettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal TaxablePercentage { get; init; }
    }

    public sealed record StandardDeductionSettings
    {
        public required decimal Amount { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal AnnualRateOfIncrease { get; init; }
    }

    public sealed record HealthcareDeductionSettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal AgiFloorPercentage { get; init; }
    }

    public sealed record CharityDeductionSettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal AgiReductionPercentage { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal AgiLimitPercentage { get; init; }
    }

    public sealed record SaltDeductionSettings
    {
        public required decimal MaximumAmount { get; init; }
        public required decimal AgiPhaseoutThreshold { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal PhaseoutPercentage { get; init; }
        public required int LimitChangeYear { get; init; }
        public required decimal MaximumAmountAfterChange { get; init; }
    }

    public sealed record OrdinaryIncomeTaxSettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal ThresholdAnnualRateOfIncrease { get; init; }
        public required ImmutableArray<Bracket> Brackets { get; init; }
    }

    public sealed record CapitalGainsTaxSettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal ThresholdAnnualRateOfIncrease { get; init; }
        public required ImmutableArray<Bracket> Brackets { get; init; }
    }

    public sealed record NetInvestmentIncomeTaxSettings
    {
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal Rate { get; init; }
        public required decimal AgiThreshold { get; init; }
    }

    public sealed record AlternativeMinimumTaxSettings
    {
        public required decimal ExemptionAmount { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal ExemptionAnnualRateOfIncrease { get; init; }
        public required decimal Threshold { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal ThresholdAnnualRateOfIncrease { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal LowerRate { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal UpperRate { get; init; }
    }

    public sealed record Bracket
    {
        public required decimal Threshold { get; init; }
        [JsonConverter(typeof(PercentageJsonConverter))]
        public required decimal Rate { get; init; }
    }
}
