namespace FinPlanner.Data;

public class Portfolio
{
    public List<Account> Accounts { get; set; } = new();
    public List<IncomeItem> Income { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public decimal Year { get; set; } = 0;
    public decimal SecuritiesAnnualInterestRate { get; set; } = 0.0m;
    public decimal AnnualInflationRate { get; set; } = 0.0m;
    public int CurrentAge { get; set; } = 0;
    public int LifeExpectancy { get; set; } = 0;
    public decimal AnnualExpenses { get; set; } = 0.0m;
    public decimal BondsAnnualRateOfReturn { get; set; } = 0.0m;

    public event Action? Changed;

    public void NotifyStateChanged()
    {
        Changed?.Invoke();
    }

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

        NotifyStateChanged();
    }

    public void AddIncome(string name, decimal amount)
    {
        Income.Add(new IncomeItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
        NotifyStateChanged();
    }

    public void AddExpense(string name, decimal amount, int yearStart, int yearEnd, decimal annualRateOfIncrease)
    {
        Expenses.Add(new Expense
        {
            Name = name,
            Amount = amount,
            YearStart = yearStart,
            YearEnd = yearEnd,
            AnnualRateOfIncrease = annualRateOfIncrease,
            LastUpdated = DateTime.Now
        });
        NotifyStateChanged();
    }
}