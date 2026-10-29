namespace FinPlanner.Engine;

/// <summary>
/// Investment earnings from one account during a plan year. Distributions of
/// existing funds are recorded separately as AccountTransfer results.
/// </summary>
public class AccountEarnings
{
    private const decimal InterestRate = 0.0017m;
    private const decimal DivQRate = 0.0094m;
    private const decimal DivUQRate = 0.0005m;
    internal const decimal BrokerageEarnedRate = InterestRate + DivQRate + DivUQRate;

    public Account Account { get; }
    /// <summary>
    /// Brokerage interest on the beginning balance: 0.17% for Equities, or the
    /// scenario's BondsAnnualRateOfReturn (stored in AppreciationRate) for Bonds.
    /// </summary>
    public decimal Interest { get; private set; }
    /// <summary>Qualified Brokerage Equities dividends: 0.94% of the beginning balance.</summary>
    public decimal DivQ { get; private set; }
    /// <summary>Unqualified Brokerage Equities dividends: 0.05% of the beginning balance.</summary>
    public decimal DivUQ { get; private set; }
    /// <summary>New earnings, added on top of account appreciation.</summary>
    public decimal EarnedAmount => Interest + DivQ + DivUQ;
    public decimal Amount => EarnedAmount;

    public AccountEarnings(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        Account = account;
        if (HasBrokerageEquityIncome(account))
        {
            var balance = Math.Max(0m, account.BeginningBalance);
            Interest = Round(balance * InterestRate);
            DivQ = Round(balance * DivQRate);
            DivUQ = Round(balance * DivUQRate);
        }
        else if (account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Bonds)
        {
            // PlanYear assigns the JSON scenario's bond rate before constructing income.
            Interest = Round(Math.Max(0m, account.BeginningBalance)
                * Math.Max(0m, account.AppreciationRate));
        }
    }

    private static decimal Round(decimal amount)
        => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    internal static bool HasBrokerageEquityIncome(Account account)
        => account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Equities;

    internal static decimal GetEarnedRate(Account account)
    {
        if (HasBrokerageEquityIncome(account))
        {
            return BrokerageEarnedRate;
        }
        if (account.Type == AccountType.Brokerage && account.Holdings == AccountHoldings.Bonds)
        {
            return Math.Max(0m, account.AppreciationRate);
        }
        return 0m;
    }

    internal static decimal GetTotalReturn(Account account)
        => account.AppreciationRate + GetEarnedRate(account);
}
