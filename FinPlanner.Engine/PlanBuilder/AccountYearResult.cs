namespace FinPlanner.Engine;

/// <summary>
/// This represents what happened to one account during one calculated year.
/// </summary>namespace FinPlanner.Engine;

public sealed class AccountYearResult
{
    public required Guid AccountId { get; init; }

    public required string AccountName { get; init; }

    public decimal BeginningBalance { get; init; }

    public decimal IncomeDeposits { get; init; }

    public decimal TransfersIn { get; init; }

    public decimal TransfersOut { get; init; }

    public decimal ExpenseWithdrawals { get; init; }

    public decimal TaxWithdrawals { get; init; }

    public decimal QualifiedDividends { get; init; }

    public decimal NonqualifiedDividends { get; init; }

    public decimal Interest { get; init; }

    public decimal CapitalAppreciation { get; init; }

    public decimal EndingBalance { get; init; }

    public decimal TotalDividends =>
        QualifiedDividends + NonqualifiedDividends;

    public decimal TotalTaxableInvestmentIncome =>
        TotalDividends + Interest;

    public decimal TotalInvestmentReturn =>
        TotalTaxableInvestmentIncome + CapitalAppreciation;

    public decimal NetCashFlow =>
        IncomeDeposits
        + TransfersIn
        - TransfersOut
        - ExpenseWithdrawals
        - TaxWithdrawals;

    public decimal CalculatedEndingBalance =>
        BeginningBalance
        + NetCashFlow
        + TotalInvestmentReturn;
}