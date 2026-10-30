namespace FinPlanner.Engine;

/// <summary>
/// Investment earnings from one account during a plan year. Distributions of
/// existing funds are recorded separately as AccountTransfer results.
/// </summary>
public class AccountEarnings
{
    public Account Account { get; }
    /// <summary>
    /// Brokerage interest on the beginning balance: 0.17% for Equities, or the
    /// scenario's BondsAnnualRateOfReturn (stored in AppreciationRate) for Bonds.
    /// </summary>
    public decimal Interest { get; }
    /// <summary>Qualified Brokerage Equities dividends: 0.94% of the beginning balance.</summary>
    public decimal DivQ { get; }
    /// <summary>Unqualified Brokerage Equities dividends: 0.05% of the beginning balance.</summary>
    public decimal DivUQ { get; }
    /// <summary>New earnings, added on top of account appreciation.</summary>
    public decimal EarnedAmount => Interest + DivQ + DivUQ;
    public decimal Amount => EarnedAmount;

    internal AccountEarnings(Account account, decimal interest, decimal divQ, decimal divUQ)
    {
        ArgumentNullException.ThrowIfNull(account);
        Account = account;
        Interest = interest;
        DivQ = divQ;
        DivUQ = divUQ;
    }
}
