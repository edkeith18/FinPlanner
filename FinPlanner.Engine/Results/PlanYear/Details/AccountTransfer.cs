namespace FinPlanner.Engine;

public enum TransferReason { Rmd, Scheduled, ExpenseWithdrawal, SurplusReinvestment, ExternalIncome }

/// <summary>
/// One movement of funds in a plan period. External income has no account endpoints.
/// A null endpoint represents
/// the period's cash pool for expenses; account-to-account transfers conserve wealth.
/// RequestedAmount preserves the instruction; Amount starts at the requested value
/// and records the amount actually moved once the calculator applies the transfer.
/// </summary>
public class AccountTransfer
{
    public string Name { get; }
    public Account? FromAccount { get; }
    public Account? ToAccount { get; }
    public TransferReason Reason { get; }
    public decimal RequestedAmount { get; }
    public decimal Amount { get; internal set; }
    public decimal MandatoryAmount => Reason == TransferReason.Rmd ? Amount : 0m;
    public decimal VoluntaryAmount => Reason == TransferReason.ExpenseWithdrawal ? Amount : 0m;

    public AccountTransfer(string name, Account? fromAccount, Account? toAccount,
        decimal amount, TransferReason reason)
    {
        if (fromAccount is null && toAccount is null && reason != TransferReason.ExternalIncome)
            throw new ArgumentException("A transfer needs at least one account endpoint.");
        if (reason == TransferReason.ExternalIncome && (fromAccount is not null || toAccount is not null))
            throw new ArgumentException("External income must flow directly into the cash pool.");
        if (fromAccount is not null && fromAccount.Id == toAccount?.Id)
            throw new ArgumentException("Transfer source and destination must differ.");
        Name = name;
        FromAccount = fromAccount;
        ToAccount = toAccount;
        Reason = reason;
        RequestedAmount = Math.Max(0m, amount);
        Amount = RequestedAmount;
    }
}
