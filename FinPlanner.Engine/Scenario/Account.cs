namespace FinPlanner.Engine;

public class Account
{

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Brokerage;
    public AccountHoldings Holdings { get; set; } = AccountHoldings.Equities;
    public decimal BeginningBalance { get; set; }
    public decimal EndingBalance { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Determines the order in which funds are withdrawn from this account.
    /// Lower values have higher priority; priority 1 is used first.
    /// </summary>
    public int WithdrawalPriority { get; set; }

    public Account() { }

    // Copy constructor to create a new Account instance from an existing one
    public Account(Account sourceAccount)
    {
        Id = sourceAccount.Id;
        Name = sourceAccount.Name;
        Type = sourceAccount.Type;
        Holdings = sourceAccount.Holdings;
        BeginningBalance = sourceAccount.BeginningBalance;
        // Do not copy EndingBalance here, as it will be calculated later in the plan year.
        LastUpdated = sourceAccount.LastUpdated;
        WithdrawalPriority = sourceAccount.WithdrawalPriority;
    }
}

public enum AccountType
{
    Brokerage,
    Hsa,
    RetirementRoth,
    Retirement,
    Inherited
}

public enum AccountHoldings
{
    Equities,
    Bonds
}

public static class AccountOptionDisplay
{
    public static IReadOnlyList<AccountType> Types { get; } = Enum.GetValues<AccountType>();

    public static IReadOnlyList<AccountHoldings> Holdings { get; } = Enum.GetValues<AccountHoldings>();

    public static string ToDisplayLabel(this AccountType type)
    {
        return type switch
        {
            AccountType.Brokerage => "Brokerage",
            AccountType.Hsa => "HSA",
            AccountType.RetirementRoth => "Retirement (Roth)",
            AccountType.Retirement => "Retirement",
            AccountType.Inherited => "Inherited",
            _ => "Brokerage"
        };
    }

    public static string ToDisplayLabel(this AccountHoldings holdings)
    {
        return holdings switch
        {
            AccountHoldings.Equities => "Equities",
            AccountHoldings.Bonds => "Bonds",
            _ => "Equities"
        };
    }
}