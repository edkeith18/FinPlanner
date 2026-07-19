namespace FinPlanner.Engine;

/// <summary>
/// Mutable working values for one account during one calculated year.
/// </summary>
internal sealed class AccountYearCalculation
{
    public required Guid AccountId { get; init; }
    public required string AccountName { get; init; }
    public decimal BeginningBalance { get; init; }
    public decimal IncomeDeposits { get; set; }
    public decimal TransfersIn { get; set; }
    public decimal TransfersOut { get; set; }
    public decimal ExpenseWithdrawals { get; set; }
    public decimal TaxWithdrawals { get; set; }
    public decimal QualifiedDividends { get; set; }
    public decimal NonqualifiedDividends { get; set; }
    public decimal Interest { get; set; }
    public decimal CapitalAppreciation { get; set; }

    public decimal EndingBalance =>
        BeginningBalance
        + IncomeDeposits
        + TransfersIn
        - TransfersOut
        - ExpenseWithdrawals
        - TaxWithdrawals
        + QualifiedDividends
        + NonqualifiedDividends
        + Interest
        + CapitalAppreciation;

    public AccountYearResult ToResult()
    {
        return new AccountYearResult
        {
            AccountId = AccountId,
            AccountName = AccountName,
            BeginningBalance = BeginningBalance,
            IncomeDeposits = IncomeDeposits,
            TransfersIn = TransfersIn,
            TransfersOut = TransfersOut,
            ExpenseWithdrawals = ExpenseWithdrawals,
            TaxWithdrawals = TaxWithdrawals,
            QualifiedDividends = QualifiedDividends,
            NonqualifiedDividends = NonqualifiedDividends,
            Interest = Interest,
            CapitalAppreciation = CapitalAppreciation,
            EndingBalance = EndingBalance
        };
    }
}
