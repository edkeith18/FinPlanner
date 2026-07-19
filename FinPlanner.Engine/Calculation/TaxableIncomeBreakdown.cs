namespace FinPlanner.Engine;

/// <summary>
/// Represents the taxable income received during a single tax year.
///
/// The individual categories are used to calculate Adjusted Gross Income (AGI),
/// taxable income, and taxes. Different categories may be taxed under different
/// rules (for example, qualified dividends and long-term capital gains).
/// </summary>
public sealed class TaxableIncomeBreakdown
{
    /// <summary>
    /// Wages and salary reported on Form W-2.
    /// </summary>
    public decimal Wages { get; init; }

    /// <summary>
    /// Taxable interest income.
    /// </summary>
    public decimal TaxableInterest { get; init; }

    /// <summary>
    /// Ordinary (non-qualified) dividend income.
    /// </summary>
    public decimal OrdinaryDividends { get; init; }

    /// <summary>
    /// Qualified dividend income.
    /// </summary>
    public decimal QualifiedDividends { get; init; }

    /// <summary>
    /// Net short-term capital gains.
    /// </summary>
    public decimal ShortTermCapitalGains { get; init; }

    /// <summary>
    /// Net long-term capital gains.
    /// </summary>
    public decimal LongTermCapitalGains { get; init; }

    /// <summary>
    /// Taxable distributions from IRAs and employer retirement plans.
    /// </summary>
    public decimal TaxableRetirementDistributions { get; init; }

    /// <summary>
    /// Taxable portion of Social Security benefits.
    /// </summary>
    public decimal TaxableSocialSecurityBenefits { get; init; }

    /// <summary>
    /// Taxable pension income.
    /// </summary>
    public decimal PensionIncome { get; init; }

    /// <summary>
    /// Other taxable income not represented by another category.
    /// </summary>
    public decimal OtherTaxableIncome { get; init; }

    /// <summary>
    /// Total taxable income before adjustments to income.
    /// </summary>
    public decimal Total =>
        Wages
        + TaxableInterest
        + OrdinaryDividends
        + QualifiedDividends
        + ShortTermCapitalGains
        + LongTermCapitalGains
        + TaxableRetirementDistributions
        + TaxableSocialSecurityBenefits
        + PensionIncome
        + OtherTaxableIncome;
}