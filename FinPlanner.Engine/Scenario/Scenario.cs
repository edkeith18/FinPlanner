using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

public class Scenario
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public List<Account> Accounts { get; set; } = new();
    public List<IncomeItem> Income { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<Transfer> Transfers { get; set; } = new();
    [JsonPropertyName("Year")]
    public int StartYear { get; set; } = DateTime.Now.Year;
    [JsonIgnore]
    public int EndYear => StartYear + Math.Max(0, LifeExpectancy - CurrentAge);
    public decimal SecuritiesAnnualInterestRate { get; set; }
    public decimal AnnualInflationRate { get; set; }
    public int CurrentAge { get; set; }
    public int LifeExpectancy { get; set; }
    public decimal AnnualExpenses { get; set; }
    public decimal BondsAnnualRateOfReturn { get; set; }

    public event Action? Changed;

    public string Serialize()
    {
        NormalizeWithdrawalPriorities();
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    public async Task SerializeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        NormalizeWithdrawalPriorities();

        await JsonSerializer.SerializeAsync(
            stream,
            this,
            SerializerOptions,
            cancellationToken);
    }

    public static Scenario Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var scenario = JsonSerializer.Deserialize<Scenario>(
            json,
            SerializerOptions);

        scenario = scenario
            ?? throw new JsonException(
                "The JSON did not contain a valid scenario.");

        scenario.NormalizeWithdrawalPriorities();
        return scenario;
    }

    public static async Task<Scenario> DeserializeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var scenario = await JsonSerializer.DeserializeAsync<Scenario>(
            stream,
            SerializerOptions,
            cancellationToken);

        scenario = scenario
            ?? throw new JsonException(
                "The JSON did not contain a valid scenario.");

        scenario.NormalizeWithdrawalPriorities();
        return scenario;
    }

    public void NotifyStateChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>
    /// Synchronizes each account's persisted withdrawal priority with its
    /// position in <see cref="Accounts"/>. Account-list order is authoritative.
    /// </summary>
    public void NormalizeWithdrawalPriorities()
    {
        for (var index = 0; index < Accounts.Count; index++)
        {
            Accounts[index].WithdrawalPriority = index + 1;
        }
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

        NormalizeWithdrawalPriorities();
        NotifyStateChanged();
    }

    public void AddIncome(string name, decimal amount)
    {
        Income.Add(new IncomeItem { Name = name, Amount = amount, LastUpdated = DateTime.Now });
        NotifyStateChanged();
    }

    public void AddExpense(string name, decimal amount, int ageStart, int ageEnd, decimal annualRateOfIncrease, bool useInflationValue = true)
    {
        Expenses.Add(new Expense
        {
            Name = name,
            Amount = amount,
            AgeStart = ageStart,
            AgeEnd = ageEnd,
            AnnualRateOfIncrease = annualRateOfIncrease,
            UseInflationValue = useInflationValue,
            LastUpdated = DateTime.Now
        });
        NotifyStateChanged();
    }

    public void AddTransfer(string name, decimal amount, int ageStart, int ageEnd, string fromAccountName, string toAccountName, decimal annualRateOfIncrease, bool useInflationValue = true)
    {
        Transfers.Add(new Transfer
        {
            Name = name,
            Amount = amount,
            AgeStart = ageStart,
            AgeEnd = ageEnd,
            FromAccountName = fromAccountName,
            ToAccountName = toAccountName,
            AnnualRateOfIncrease = annualRateOfIncrease,
            UseInflationValue = useInflationValue
        });
        NotifyStateChanged();
    }
}
