namespace FinPlanner.Data;

public class Portfolio
{
    public List<Account> Accounts { get; set; } = new();
    public List<IncomeItem> Income { get; set; } = new();
    public List<ExpenseItem> Expenses { get; set; } = new();
    public decimal Year { get; set; } = 0;
    public decimal SecuritiesAnnualInterestRate { get; set; } = 0.0m;
    public decimal AnnualInflationRate { get; set; } = 0.0m;
    public int CurrentAge { get; set; } = 0;
    public int LifeExpectancy { get; set; } = 0;
    public decimal AnnualExpenses { get; set; } = 0.0m;
    public decimal BondsAnnualRateOfReturn { get; set; } = 0.0m;

    public void AddAccount(string name, decimal balance, AccountType type = AccountType.Brokerage, AccountHoldings holdings = AccountHoldings.Equities)
    {
        Accounts.Add(new Account
        {
            Name = name,
            Type = type,
            Holdings = holdings,
            Balance = balance,
            LastUpdated = DateTime.Now
        });
    }

    public void AddIncome(string name, decimal amount)
    {
        Income.Add(new IncomeItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
    }

    public void AddExpense(string name, decimal amount)
    {
        Expenses.Add(new ExpenseItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
    }
}

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
    public static IReadOnlyList<AccountType> Types { get; } = new[]
    {
        AccountType.Brokerage,
        AccountType.Hsa,
        AccountType.RetirementRoth,
        AccountType.Retirement,
        AccountType.Inherited
    };

    public static IReadOnlyList<AccountHoldings> Holdings { get; } = new[]
    {
        AccountHoldings.Equities,
        AccountHoldings.Bonds
    };

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

public class ExpenseItem
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime LastUpdated { get; set; }
}
