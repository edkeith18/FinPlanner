namespace FinPlanner.Engine;

public class Transfer
{
    public string Name { get; set; } = "";
    public string FromAccountName { get; set; } = "";
    public string ToAccountName { get; set; } = "";
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal Amount { get; set; }
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    public decimal Execute(IEnumerable<Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var accountList = accounts as IList<Account> ?? accounts.ToList();
        var fromAccount = FindAccount(accountList, FromAccountName, nameof(FromAccountName));
        var toAccount = FindAccount(accountList, ToAccountName, nameof(ToAccountName));

        if (Amount <= 0m || fromAccount.Balance <= 0m)
        {
            return 0m;
        }

        var transferAmount = Math.Min(Amount, fromAccount.Balance);

        fromAccount.Balance -= transferAmount;
        toAccount.Balance += transferAmount;

        return transferAmount;
    }

    private static Account FindAccount(IEnumerable<Account> accounts, string accountName, string propertyName)
    {
        var account = accounts.FirstOrDefault(account => account.Name == accountName);

        if (account is null)
        {
            throw new InvalidOperationException($"Transfer account '{accountName}' from {propertyName} could not be found.");
        }

        return account;
    }
}
