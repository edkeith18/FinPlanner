using FinPlanner.Engine;

namespace FinPlanner.Web.Services;

public sealed record ForecastColumn(string Label, Func<PlanYear, decimal?> Value, bool IsRate = false);

public static class ForecastBreakdown
{
    public static decimal TotalIncome(PlanYear year) => year.ExternalIncome + year.AccountEarnings.Sum(e => e.Amount);

    public static List<ForecastColumn> Income(IEnumerable<PlanYear> years)
    {
        var rows = years.ToList();
        var columns = rows.SelectMany(y => y.Transfers)
            .Where(t => t.Reason == TransferReason.ExternalIncome).Select(t => t.Name).Distinct()
            .Select(name => new ForecastColumn(name, y => y.Transfers
                .Where(t => t.Reason == TransferReason.ExternalIncome && t.Name == name).Sum(t => t.Amount))).ToList();
        foreach (var account in rows.SelectMany(y => y.AccountEarnings).Select(e => e.Account).DistinctBy(a => a.Id))
        {
            var id = account.Id;
            columns.Add(new($"{account.Name} · Interest", y => y.AccountEarnings.Where(e => e.Account.Id == id).Sum(e => e.Interest)));
            columns.Add(new($"{account.Name} · Qualified dividends", y => y.AccountEarnings.Where(e => e.Account.Id == id).Sum(e => e.DivQ)));
            columns.Add(new($"{account.Name} · Unqualified dividends", y => y.AccountEarnings.Where(e => e.Account.Id == id).Sum(e => e.DivUQ)));
        }
        return columns;
    }

    public static List<ForecastColumn> Expenses(IEnumerable<PlanYear> years) => years.SelectMany(y => y.Expenses)
        .Where(e => e.Type != ExpenseType.Tax).Select(e => e.Name).Distinct()
        .Select(name => new ForecastColumn(name, y => y.Expenses
            .Where(e => e.Type != ExpenseType.Tax && e.Name == name).Sum(e => e.Amount))).ToList();

    public static List<ForecastColumn> Transfers(IEnumerable<PlanYear> years) => years.SelectMany(y => y.Transfers)
        .Where(t => t.Reason != TransferReason.ExternalIncome)
        .GroupBy(t => (t.Name, t.Reason, From: t.FromAccount?.Id, To: t.ToAccount?.Id))
        .Select(group =>
        {
            var transfer = group.First();
            var reason = transfer.Reason switch
            {
                TransferReason.Rmd => "Required minimum distribution",
                TransferReason.Scheduled => "Scheduled",
                TransferReason.ExpenseWithdrawal => "Expense withdrawal",
                TransferReason.SurplusReinvestment => "Surplus reinvestment",
                _ => transfer.Reason.ToString()
            };
            var label = $"{transfer.Name} · {reason} · {transfer.FromAccount?.Name ?? "Cash pool"} → {transfer.ToAccount?.Name ?? "Cash pool"}";
            return new ForecastColumn(label, y => y.Transfers
                .Where(t => (t.Name, t.Reason, t.FromAccount?.Id, t.ToAccount?.Id) == group.Key)
                .Sum(t => t.Amount));
        }).ToList();

    public static IReadOnlyList<ForecastColumn> Taxes { get; } =
    [
        new("Ordinary income", y => y.Taxes?.OrdinaryIncome),
        new("Qualified dividends", y => y.Taxes?.QualifiedDividends),
        new("Unqualified dividends", y => y.Taxes?.UnqualifiedDividends),
        new("Interest", y => y.Taxes?.Interest),
        new("Long-term gains", y => y.Taxes?.LongTermCapitalGains),
        new("Short-term gains", y => y.Taxes?.ShortTermCapitalGains),
        new("Adjusted gross income", y => y.Taxes?.AGI),
        new("HSA contribution", y => y.Taxes?.HsaContribution),
        new("Healthcare deduction", y => y.Taxes?.HealthcareDeduction),
        new("Charity deduction", y => y.Taxes?.CharityDeduction),
        new("SALT deduction", y => y.Taxes?.SaltDeduction),
        new("Standard deduction", y => y.Taxes?.StandardDeduction),
        new("Applied deductions", y => y.Taxes?.Deductions),
        new("Taxable ordinary income", y => y.Taxes?.TaxableOrdinaryIncome),
        new("Taxable capital gains", y => y.Taxes?.TaxableCapitalGains),
        new("Ordinary tax", y => y.Taxes?.OrdinaryTax),
        new("Capital gains tax", y => y.Taxes?.LTCGTax),
        new("Alternative minimum tax", y => y.Taxes?.AMT),
        new("Net investment income tax", y => y.Taxes?.NIIT),
        new("Calculated total tax", y => y.Taxes?.TotalTax),
        new("Effective tax rate", y => y.Taxes?.EffectiveTaxRate, true)
    ];
}
