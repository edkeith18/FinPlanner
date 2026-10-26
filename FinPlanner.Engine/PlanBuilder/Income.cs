namespace FinPlanner.Engine;

/// <summary>
/// Withdrawals from one account during a plan year.
/// </summary>
public class Income
{
    public Account Account { get; }
    public decimal MandatoryAmount { get; internal set; }
    public decimal VoluntaryAmount { get; internal set; }
    public decimal Amount => MandatoryAmount + VoluntaryAmount;

    public Income(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        Account = account;
        MandatoryAmount = Math.Max(0m, decimal.Round(
            account.BeginningBalance * GetMandatoryRate(account.Type),
            0, MidpointRounding.AwayFromZero));
    }

    internal static decimal GetMandatoryRate(AccountType type)
        => type == AccountType.Inherited ? 0.15m : 0m;
}
