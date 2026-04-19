namespace FinPlanner.Data;

public class Portfolio
{
    public List<Account> Accounts { get; set; } = new();
    public List<IncomeItem> Income { get; set; } = new();
    public List<ExpenseItem> Expenses { get; set; } = new();
    public decimal SecuritiesAnnualInterestRate { get; set; }
    public decimal Year { get; set; } = 0;

    public void AddAccount(string name, decimal balance)
    {
        Accounts.Add(new Account { Name = name, Balance = balance, LastUpdated = DateTime.Now });
    }

    public void AddIncome(string name, decimal amount)
    {
        Income.Add(new IncomeItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
    }

    public void AddExpense(string name, decimal amount)
    {
        Expenses.Add(new ExpenseItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
    }

    public void LoadFrom(Portfolio? data)
    {
        if (data is null)
        {
            return;
        }

        Accounts = data.Accounts ?? new();
        Income = data.Income ?? new();
        Expenses = data.Expenses ?? new();
        SecuritiesAnnualInterestRate = data.SecuritiesAnnualInterestRate;
        Year = data.Year;
    }
}

public class Account
{
    public string Name { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime LastUpdated { get; set; }
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
