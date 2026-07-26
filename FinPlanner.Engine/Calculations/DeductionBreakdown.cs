namespace FinPlanner.Engine;

/// <summary>
/// Represents the deductions available for a single tax year.
///
/// This includes both the standard deduction and the individual
/// itemized deductions used to determine which deduction produces
/// the lowest tax liability.
/// </summary>
public sealed class DeductionBreakdown
{
    /// <summary>
    /// The standard deduction available for the taxpayer's filing status.
    /// </summary>
    public decimal StandardDeduction { get; init; }

    /// <summary>
    /// State and local taxes (SALT), subject to any applicable limits.
    /// </summary>
    public decimal StateAndLocalTaxes { get; init; }

    /// <summary>
    /// Qualified mortgage interest deduction.
    /// </summary>
    public decimal MortgageInterest { get; init; }

    /// <summary>
    /// Qualified charitable contributions.
    /// </summary>
    public decimal CharitableContributions { get; init; }

    /// <summary>
    /// Deductible medical expenses.
    /// </summary>
    public decimal MedicalExpenses { get; init; }

    /// <summary>
    /// Other itemized deductions not represented individually.
    /// </summary>
    public decimal OtherItemizedDeductions { get; init; }

    /// <summary>
    /// Total itemized deductions.
    /// </summary>
    public decimal TotalItemizedDeductions =>
        StateAndLocalTaxes
        + MortgageInterest
        + CharitableContributions
        + MedicalExpenses
        + OtherItemizedDeductions;

    /// <summary>
    /// The deduction actually used by the tax calculation.
    /// Normally this is the greater of the standard deduction
    /// and the total itemized deductions.
    /// </summary>
    public decimal DeductionUsed =>
        Math.Max(StandardDeduction, TotalItemizedDeductions);
}