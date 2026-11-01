namespace FinPlanner.Engine;

/// <summary>Annual results of the VBA tax model.</summary>
public sealed class TaxResult
{
    public decimal OrdinaryIncome { get; init; }
    public decimal QualifiedDividends { get; init; }
    public decimal UnqualifiedDividends { get; init; }
    public decimal Interest { get; init; }
    public decimal LongTermCapitalGains { get; init; }
    public decimal ShortTermCapitalGains { get; init; }
    public decimal HsaContribution { get; init; }
    public decimal HealthcareDeduction { get; init; }
    public decimal CharityDeduction { get; init; }
    public decimal SaltDeduction { get; init; }
    public decimal StandardDeduction { get; init; }
    public decimal AGI { get; init; }
    public decimal Deductions { get; init; }
    public decimal TaxableOrdinaryIncome { get; init; }
    public decimal TaxableCapitalGains { get; init; }
    public decimal OrdinaryTax { get; init; }
    public decimal LTCGTax { get; init; }
    public decimal StandardTax => OrdinaryTax + LTCGTax;
    public decimal AMT { get; init; }
    public decimal NIIT { get; init; }
    public decimal TotalTax => StandardTax + AMT + NIIT;
    public decimal EffectiveTaxRate => AGI > 0m ? TotalTax / AGI : 0m;
    /// <summary>The VBA calls this marginal, but computes total tax divided by AGI.</summary>
    public decimal MarginalTaxRate => EffectiveTaxRate;
}
