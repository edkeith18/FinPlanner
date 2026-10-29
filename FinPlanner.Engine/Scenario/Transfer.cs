using System.Text.Json.Serialization;

﻿namespace FinPlanner.Engine;

public class Transfer
{
    public string Name { get; set; } = "";
    public string FromAccountName { get; set; } = "";
    public string ToAccountName { get; set; } = "";
    public int AgeStart { get; set; }
    public int AgeEnd { get; set; }
    public decimal Amount { get; set; }
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal AnnualRateOfIncrease { get; set; }
    public bool UseInflationValue { get; set; } = true;

    public Transfer() { }

    public Transfer(Transfer source)
    {
        Name = source.Name;
        FromAccountName = source.FromAccountName;
        ToAccountName = source.ToAccountName;
        AgeStart = source.AgeStart;
        AgeEnd = source.AgeEnd;
        Amount = source.Amount;
        AnnualRateOfIncrease = source.AnnualRateOfIncrease;
        UseInflationValue = source.UseInflationValue;
    }

    /// <summary>Forecast from the original amount, applying inclusive age boundaries.</summary>
    public decimal ForecastAmount(int planStartAge, int age, decimal inflationRate)
    {
        if (age < AgeStart || age > AgeEnd) return 0m;
        var factor = 1m;
        var rate = UseInflationValue ? inflationRate : AnnualRateOfIncrease;
        for (var current = planStartAge; current < age; current++) factor *= 1m + rate;
        return decimal.Round(Amount * factor, 0, MidpointRounding.AwayFromZero);
    }

    public decimal Execute(IEnumerable<Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var accountList = accounts as IList<Account> ?? accounts.ToList();
        var fromAccount = FindAccount(accountList, FromAccountName, nameof(FromAccountName));
        var toAccount = FindAccount(accountList, ToAccountName, nameof(ToAccountName));

        if (Amount <= 0m || fromAccount.BeginningBalance <= 0m)
        {
            return 0m;
        }

        var transferAmount = Math.Min(Amount, fromAccount.BeginningBalance);

        fromAccount.BeginningBalance -= transferAmount;
        toAccount.BeginningBalance += transferAmount;

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
