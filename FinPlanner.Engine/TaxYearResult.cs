namespace FinPlanner.Engine;

/// <summary>
/// Represents the federal and state tax calculation for a single calendar year.
///
/// This class captures the major values used to calculate taxes and provides
/// intermediate results (AGI, taxable income, total tax, etc.) so they can
/// be displayed and inspected by the user.
/// </summary>
public sealed class TaxYearResult
{
    /// <summary>
    /// Total gross income from all sources before adjustments.
    /// </summary>
    public decimal GrossIncome { get; init; }

    /// <summary>
    /// Above-the-line deductions (IRA contributions, HSA contributions, etc.).
    /// </summary>
    public decimal AdjustmentsToIncome { get; init; }

    /// <summary>
    /// Adjusted Gross Income (AGI).
    /// </summary>
    public decimal AdjustedGrossIncome =>
        GrossIncome - AdjustmentsToIncome;

    /// <summary>
    /// The standard deduction available for the filing status.
    /// </summary>
    public decimal StandardDeduction { get; init; }

    /// <summary>
    /// Total itemized deductions.
    /// </summary>
    public decimal ItemizedDeductions { get; init; }

    /// <summary>
    /// The deduction actually used on the tax return.
    /// Normally the greater of the standard deduction or itemized deductions.
    /// </summary>
    public decimal DeductionUsed =>
        Math.Max(StandardDeduction, ItemizedDeductions);

    /// <summary>
    /// Qualified Business Income (QBI) deduction.
    /// </summary>
    public decimal QualifiedBusinessIncomeDeduction { get; init; }

    /// <summary>
    /// Taxable income after deductions.
    /// </summary>
    public decimal TaxableIncome =>
        Math.Max(
            0m,
            AdjustedGrossIncome
            - DeductionUsed
            - QualifiedBusinessIncomeDeduction);

    /// <summary>
    /// Regular federal income tax before additional taxes.
    /// </summary>
    public decimal RegularIncomeTax { get; init; }

    /// <summary>
    /// Additional Alternative Minimum Tax (AMT) owed.
    /// This should represent only the excess AMT above the regular tax.
    /// </summary>
    public decimal AdditionalAlternativeMinimumTax { get; init; }

    /// <summary>
    /// Net Investment Income Tax (NIIT).
    /// </summary>
    public decimal NetInvestmentIncomeTax { get; init; }

    /// <summary>
    /// Additional Medicare Tax.
    /// </summary>
    public decimal AdditionalMedicareTax { get; init; }

    /// <summary>
    /// Self-employment tax.
    /// </summary>
    public decimal SelfEmploymentTax { get; init; }

    /// <summary>
    /// Other miscellaneous federal taxes.
    /// </summary>
    public decimal OtherFederalTaxes { get; init; }

    /// <summary>
    /// Total tax credits applied against federal tax.
    /// </summary>
    public decimal TaxCredits { get; init; }

    /// <summary>
    /// Total federal income tax after credits.
    /// </summary>
    public decimal TotalFederalTax =>
        Math.Max(
            0m,
            RegularIncomeTax
            + AdditionalAlternativeMinimumTax
            + NetInvestmentIncomeTax
            + AdditionalMedicareTax
            + SelfEmploymentTax
            + OtherFederalTaxes
            - TaxCredits);

    /// <summary>
    /// State income tax.
    /// </summary>
    public decimal StateIncomeTax { get; init; }

    /// <summary>
    /// Total taxes owed for the year.
    /// </summary>
    public decimal TotalTax =>
        TotalFederalTax + StateIncomeTax;
}