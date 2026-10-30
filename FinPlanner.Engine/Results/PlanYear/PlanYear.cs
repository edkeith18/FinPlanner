namespace FinPlanner.Engine;

/// <summary>
/// Represents the financial results for a single 12-month period.
/// </summary>
public class PlanYear
{
    public decimal UnfundedTransfers { get; internal set; }

    /// <summary>Expenses left unpaid after applying available cash and account withdrawals.</summary>
    public decimal UnfundedExpenses { get; internal set; }

    /// <summary>Whether the calculated year funded all expenses and scheduled transfers.</summary>
    public bool IsFullyFunded => UnfundedExpenses == 0m && UnfundedTransfers == 0m;

    /// <summary>
    /// The calendar year represented by this PlanYear at the start of the 12-month period.
    /// </summary>
    public int StartingYear { get; init; }

    /// <summary>
    /// The user's age at the start of the 12-month period.
    /// </summary>
    public int StartingAge { get; init; }

    /// <summary>
    /// The calendar year at the end of the 12-month period.
    /// </summary>
    public int EndingYear => StartingYear + 1;

    /// <summary>
    /// The user's age at the end of the 12-month period.
    /// </summary>
    public int EndingAge => StartingAge + 1;

    /// <summary>
    /// The accounts in the plan for this year.
    /// </summary>
    public List<Account> Accounts { get; set; } = new List<Account>();

    /// <summary>
    /// Earned investment income, with one entry per account.
    /// </summary>
    public List<AccountEarnings> AccountEarnings { get; internal set; } = new();

    /// <summary>Individual scheduled transfers, RMDs, expense withdrawals, and cash reinvestments.</summary>
    public List<AccountTransfer> Transfers { get; internal set; } = new();

    /// <summary>External income received during this period.</summary>
    public decimal ExternalIncome => Transfers.Where(transfer => transfer.Reason == TransferReason.ExternalIncome)
        .Sum(transfer => transfer.Amount);

    public decimal MandatoryCashFlow => AccountEarnings.Sum(income => income.Amount)
        + Transfers.Sum(transfer => transfer.MandatoryAmount) + ExternalIncome;

    public decimal TotalCashFlow => AccountEarnings.Sum(income => income.Amount)
        + Transfers.Where(transfer => transfer.ToAccount is null).Sum(transfer => transfer.Amount);

    /// <summary>
    /// The expenses in the plan for this year.
    /// </summary>
    public List<Expense> Expenses { get; set; } = new List<Expense>();

    public decimal AnnualExpenses { get; set; }

    /// <summary>
    /// Total balance across all accounts at the end of the year.
    /// </summary>
    public decimal EndingBalance =>
        Accounts.Sum(account => account.EndingBalance);

    /// <summary>
    /// Total expenses that apply at this age.
    /// </summary>
    public decimal TotalExpenses =>
        Expenses.Sum(expense => expense.Amount);
}
