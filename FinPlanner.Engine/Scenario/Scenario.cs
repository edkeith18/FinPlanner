using System.Globalization;
using System.Text;
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

    private TaxInfo taxInfo = TaxInfo.Default;

    /// <summary>Validated tax assumptions loaded separately from scenario JSON.</summary>
    [JsonIgnore]
    public TaxInfo TaxInfo
    {
        get => taxInfo;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            taxInfo = value;
        }
    }

    public List<Account> Accounts { get; set; } = new();
    public List<Income> Income { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<ScheduledTransfer> Transfers { get; set; } = new();
    [JsonIgnore] 
    public int StartYear { get; set; } = DateTime.Now.Year;
    [JsonIgnore]
    public int EndYear => StartYear + LifeExpectancy - CurrentAge;
    [JsonConverter(typeof(PercentageJsonConverter))]
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
    /// <summary>Total first-year budget, including named expenses and the supplied tax expense.</summary>
    public decimal FirstYearAnnualExpenses { get; set; }
    /// <summary>Known first-year tax expense, or null to calculate it within the first-year budget.</summary>
    public decimal? FirstYearTaxExpense { get; set; } = 0m;
    [JsonConverter(typeof(PercentageJsonConverter))]
    public decimal BondsAnnualRateOfReturn { get; set; }

    public event Action? Changed;

    /// <summary>Creates a scenario with empty input collections and default tax settings.</summary>
    public Scenario() { }

    /// <summary>
    /// Copies scenario values and collection items without sharing mutable inputs
    /// or event subscriptions. Preserves the age calculated for the original scenario.
    /// </summary>
    public Scenario(Scenario source)
    {
        ArgumentNullException.ThrowIfNull(source);

        TaxInfo = source.TaxInfo;
        StartYear = source.StartYear;
        dateOfBirth = source.dateOfBirth;
        CurrentAge = source.CurrentAge;
        LifeExpectancy = source.LifeExpectancy;
        FirstYearAnnualExpenses = source.FirstYearAnnualExpenses;
        FirstYearTaxExpense = source.FirstYearTaxExpense;
        SecuritiesAnnualRateOfReturn = source.SecuritiesAnnualRateOfReturn;
        BondsAnnualRateOfReturn = source.BondsAnnualRateOfReturn;
        AnnualInflationRate = source.AnnualInflationRate;
        Accounts = source.Accounts.Select(account => new Account(account)
        {
            // A snapshot preserves exact values; rounding belongs to plan creation.
            BeginningBalance = account.BeginningBalance,
            EndingBalance = account.EndingBalance
        }).ToList();
        Expenses = source.Expenses.Select(expense => new Expense(expense)).ToList();
        Income = source.Income.Select(income => new Income
        {
            Name = income.Name,
            Type = income.Type,
            InitialAmount = income.InitialAmount,
            AgeStart = income.AgeStart,
            AnnualRateOfIncrease = income.AnnualRateOfIncrease,
            LastUpdated = income.LastUpdated
        }).ToList();
        Transfers = source.Transfers.Select(transfer => new ScheduledTransfer(transfer)).ToList();
    }

    /// <summary>Validates the planning horizon and serializes the scenario to formatted JSON.</summary>
    public string Serialize()
    {
        ValidateLifeExpectancy();
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    /// <summary>Validates the planning horizon and writes scenario JSON asynchronously to the supplied stream.</summary>
    public async Task SerializeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ValidateLifeExpectancy();

        await JsonSerializer.SerializeAsync(
            stream,
            this,
            SerializerOptions,
            cancellationToken);
    }

    /// <summary>Reads scenario JSON after rejecting negative numbers and validates the resulting inputs.</summary>
    public static Scenario Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        EnsureJsonNumbersAreNonNegative(Encoding.UTF8.GetBytes(json));

        var scenario = JsonSerializer.Deserialize<Scenario>(
            json,
            SerializerOptions);

        return ValidateDeserializedScenario(scenario);
    }

    /// <summary>Scans JSON numeric tokens and rejects negative values before deserialization.</summary>
    private static void EnsureJsonNumbersAreNonNegative(ReadOnlySpan<byte> json)
    {
        // Stream-based JSON can begin with a UTF-8 byte order mark.
        if (json.StartsWith(Encoding.UTF8.Preamble))
            json = json[Encoding.UTF8.Preamble.Length..];

        var reader = new Utf8JsonReader(json);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.Number && reader.GetDecimal() < 0m)
                throw new JsonException("JSON numbers must be non-negative.");
        }
    }

    /// <summary>Validates the planning horizon, required accounts, and explicit brokerage cost basis.</summary>
    private static Scenario ValidateDeserializedScenario(Scenario? scenario)
    {
        scenario = scenario
            ?? throw new JsonException(
                "The JSON did not contain a valid scenario.");

        scenario.ValidateLifeExpectancy();

        if (scenario.Accounts.Count == 0)
        {
            throw new ArgumentException(
                "Scenario must contain at least one account.",
                nameof(scenario));
        }

        foreach (var account in scenario.Accounts)
        {
            if (account.Type == AccountType.Brokerage && !account.HasBasis)
                throw new JsonException($"Brokerage account '{account.Name}' requires a Basis value (zero is allowed).");
        }

        return scenario;
    }

    /// <summary>Refreshes the current age and requires life expectancy to extend beyond it.</summary>
    private void ValidateLifeExpectancy()
    {
        UpdateCurrentAge();
        if (LifeExpectancy <= CurrentAge)
        {
            throw new ArgumentException(
                "Life expectancy must be greater than current age.");
        }
    }

    /// <summary>Reads a stream asynchronously, rejects negative JSON numbers, and validates the deserialized scenario.</summary>
    public static async Task<Scenario> DeserializeAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        EnsureJsonNumbersAreNonNegative(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        buffer.Position = 0;

        var scenario = await JsonSerializer.DeserializeAsync<Scenario>(
            buffer,
            SerializerOptions,
            cancellationToken);

        return ValidateDeserializedScenario(scenario);
    }

    /// <summary>
    /// Recalculates <see cref="CurrentAge"/> from <see cref="DateOfBirth"/>
    /// using the current local date.
    /// </summary>
    public void UpdateCurrentAge()
    {
        CurrentAge = DateOnly.FromDateTime(DateTime.Now).Year - DateOfBirth.Year;
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

    /// <summary>Rejects a future birth date and returns the corresponding validation message.</summary>
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

    /// <summary>Attempts to create a calendar date and returns false for invalid year, month, or day values.</summary>
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

    /// <summary>Notifies subscribers that the scenario has changed.</summary>
    public void NotifyStateChanged()
    {
        Changed?.Invoke();
    }

    /// <summary>
    /// Explicitly assigns priorities from account-list order after a user reorders accounts.
    /// Loading and saving preserve the existing priority values.
    /// </summary>
    public void NormalizeWithdrawalPriorities()
    {
        for (var index = 0; index < Accounts.Count; index++)
        {
            Accounts[index].WithdrawalPriority = index + 1;
        }
    }

    /// <summary>Adds an account after the current withdrawal priorities and notifies subscribers of the change.</summary>
    public void AddAccount(string name, decimal balance, AccountType type = AccountType.Brokerage, AccountHoldings holdings = AccountHoldings.Equities)
    {
        Accounts.Add(new Account
        {
            Name = name,
            Type = type,
            Holdings = holdings,
            BeginningBalance = balance,
            WithdrawalPriority = Accounts.Count == 0 ? 1 : Accounts.Max(account => account.WithdrawalPriority) + 1,
            LastUpdated = DateTime.Now
        });

        NotifyStateChanged();
    }

    /// <summary>Adds a named income source with its initial amount and notifies subscribers of the change.</summary>
    public void AddIncome(string name, decimal amount)
    {
        Income.Add(new Income { Name = name, InitialAmount = amount, LastUpdated = DateTime.Now });
        NotifyStateChanged();
    }

    /// <summary>Adds an expense with age boundaries and growth settings and notifies subscribers of the change.</summary>
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

    /// <summary>Adds a scheduled transfer with age boundaries and growth settings and notifies subscribers of the change.</summary>
    public void AddTransfer(string name, decimal amount, int ageStart, int ageEnd, decimal annualRateOfIncrease, bool useInflationValue = true)
    {
        Transfers.Add(new ScheduledTransfer
        {
            Name = name,
            Amount = amount,
            AgeStart = ageStart,
            AgeEnd = ageEnd,
            AnnualRateOfIncrease = annualRateOfIncrease,
            UseInflationValue = useInflationValue
        });
        NotifyStateChanged();
    }
}
