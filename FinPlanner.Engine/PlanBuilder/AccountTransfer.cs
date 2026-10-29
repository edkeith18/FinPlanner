namespace FinPlanner.Engine;

public enum TransferReason { Rmd, Scheduled, ExpenseWithdrawal, SurplusReinvestment }

/// <summary>
/// One movement of existing funds in a plan period. A null endpoint represents
/// the period's cash pool for expenses; account-to-account transfers conserve wealth.
/// Amount is the amount actually moved, while RequestedAmount preserves the instruction.
/// </summary>
public class AccountTransfer
{
    public string Name { get; }
    public Account? FromAccount { get; }
    public Account? ToAccount { get; }
    public TransferReason Reason { get; }
    public decimal RequestedAmount { get; }
    public decimal Amount { get; private set; }
    public decimal MandatoryAmount => Reason == TransferReason.Rmd ? Amount : 0m;
    public decimal VoluntaryAmount => Reason == TransferReason.ExpenseWithdrawal ? Amount : 0m;

    public AccountTransfer(string name, Account? fromAccount, Account? toAccount,
        decimal amount, TransferReason reason)
    {
        if (fromAccount is null && toAccount is null)
            throw new ArgumentException("A transfer needs at least one account endpoint.");
        if (fromAccount is not null && fromAccount.Id == toAccount?.Id)
            throw new ArgumentException("Transfer source and destination must differ.");
        Name = name;
        FromAccount = fromAccount;
        ToAccount = toAccount;
        Reason = reason;
        RequestedAmount = Math.Max(0m, amount);
        Amount = RequestedAmount;
    }

    public AccountTransfer(Account account, int startingAge)
        : this("RMD", account, null,
            RmdCalculator.Calculate(account.Type, account.BeginningBalance, startingAge), TransferReason.Rmd) { }

    internal decimal Execute()
    {
        Amount = FromAccount is null ? RequestedAmount
            : Math.Min(RequestedAmount, Math.Max(0m, FromAccount.EndingBalance));
        if (FromAccount is not null) FromAccount.EndingBalance -= Amount;
        if (ToAccount is not null) ToAccount.EndingBalance += Amount;
        return Amount;
    }

    internal static decimal GetMandatoryRate(Account account, int startingAge)
        => RmdCalculator.GetDivisor(account.Type, startingAge) is decimal divisor ? 1m / divisor : 0m;
}
