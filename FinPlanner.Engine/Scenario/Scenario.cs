using System.Globalization;
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
            new JsonStringEnumConverter(),
            new DateOfBirthJsonConverter()
        }
    };

    public List<Account> Accounts { get; set; } = new();
    public List<IncomeItem> Income { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<Transfer> Transfers { get; set; } = new();
    [JsonIgnore] 
    public int StartYear { get; set; } = DateTime.Now.AddYears(1).Year;
    [JsonIgnore]
    public int EndYear => StartYear + Math.Max(0, LifeExpectancy - CurrentAge);
    public decimal SecuritiesAnnualRateOfReturn { get; set; }
    public decimal AnnualInflationRate { get; set; }
    private DateOnly dateOfBirth = DateOnly.FromDateTime(DateTime.Now);

    [JsonConverter(typeof(DateOfBirthJsonConverter))]
    public DateOnly DateOfBirth
    {
        get => dateOfBirth;
        set
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (value > today)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Date of birth cannot be in the future.");
            }

            dateOfBirth = value;
            UpdateCurrentAge();
        }
    }

    [JsonIgnore]
    public int CurrentAge { get; private set; }
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
        scenario.UpdateCurrentAge();

        // If current age is greater than life expectancy, the scenario is invalid. This can happen if the user changes their date of birth or life expectancy in the UI.
        if (scenario.CurrentAge > scenario.LifeExpectancy)
        {
            throw new ArgumentException(
                "Current age cannot be greater than life expectancy.",
                nameof(scenario));
        }

        if (scenario.Accounts.Count == 0)
        {
            throw new ArgumentException(
                "Scenario must contain at least one account.",
                nameof(scenario));
        }

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
        scenario.UpdateCurrentAge();
        return scenario;
    }

    /// <summary>
    /// Recalculates <see cref="CurrentAge"/> from <see cref="DateOfBirth"/>
    /// using the current local date.
    /// </summary>
    public void UpdateCurrentAge()
    {
        CurrentAge = CalculateAge(DateOfBirth, DateOnly.FromDateTime(DateTime.Now));
    }

    internal static int CalculateAge(DateOnly dateOfBirth, DateOnly asOfDate)
    {
        if (dateOfBirth > asOfDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dateOfBirth),
                "Date of birth cannot be after the as-of date.");
        }

        var age = asOfDate.Year - dateOfBirth.Year;
        if (dateOfBirth.AddYears(age) > asOfDate)
        {
            age--;
        }

        return age;
    }

    /// <summary>
    /// Parses a date of birth in MM/dd/yy or MM/dd/yyyy format. Two-digit
    /// years start in the current century. A later two-digit year moves back
    /// 100 years; a later month or day in the current year remains future and
    /// is rejected.
    /// </summary>
    public static bool TryParseDateOfBirth(
        string? value,
        DateOnly today,
        out DateOnly dateOfBirth,
        out string validationMessage)
    {
        dateOfBirth = default;
        validationMessage = "Enter a valid date in MM/dd/yy or MM/dd/yyyy format.";

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (DateOnly.TryParseExact(
                value,
                "MM/dd/yyyy",
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.None,
                out dateOfBirth))
        {
            return ValidateParsedDate(dateOfBirth, today, out validationMessage);
        }

        if (value.Length != 8
            || value[2] != '/'
            || value[5] != '/'
            || !int.TryParse(value.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(value.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(value.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var shortYear))
        {
            return false;
        }

        var year = (today.Year / 100 * 100) + shortYear;
        if (shortYear > today.Year % 100)
        {
            year -= 100;
        }

        if (!TryCreateDate(year, month, day, out dateOfBirth))
        {
            return false;
        }

        return ValidateParsedDate(dateOfBirth, today, out validationMessage);
    }

    private static bool ValidateParsedDate(
        DateOnly dateOfBirth,
        DateOnly today,
        out string validationMessage)
    {
        if (dateOfBirth > today)
        {
            validationMessage = "Date of birth cannot be in the future.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private static bool TryCreateDate(int year, int month, int day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
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
