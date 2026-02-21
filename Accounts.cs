using System;

namespace FinPlanner;

public class Portfolio
{
	public List<Account> Accounts { get; set; } = new();
	public decimal Year { get; set; } = 0;

	public void AddAccount(string name, decimal balance)
	{
        Accounts.Add(new Account { Name = name, Balance = balance, LastUpdated = DateTime.Now });
    }

}

public class Account
{
	public string Name { get; set; } = "";
	public decimal Balance { get; set; }
	public DateTime LastUpdated { get; set; }
}