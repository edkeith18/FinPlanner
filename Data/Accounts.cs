namespace FinPlanner.Data;

public class Account
{
    public string Name { get; set; } = "";
    public AccountType Type { get; set; } = AccountType.Brokerage;
    public AccountHoldings Holdings { get; set; } = AccountHoldings.Equities;
    public decimal Balance { get; set; }
    public DateTime LastUpdated { get; set; }
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

public class IncomeItem
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime LastUpdated { get; set; }
}

