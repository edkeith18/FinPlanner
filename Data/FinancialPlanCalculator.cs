namespace FinPlanner.Data;

public static class FinancialPlanCalculator
{
    public static IReadOnlyList<FinancialPlanRow> BuildPlanRows(Portfolio portfolio)
    {
        return BuildPlanRows(portfolio, portfolio.AnnualExpenses);
    }

    public static IReadOnlyList<FinancialPlanRow> BuildPlanRows(Portfolio portfolio, decimal annualExpenses)
    {
        var rows = new List<FinancialPlanRow>();
        ComputeFinalBalance(portfolio, annualExpenses, rows);
        return rows;
    }

    public static decimal ComputeFinalBalance(Portfolio portfolio, decimal annualExpenses)
    {
        return ComputeFinalBalance(portfolio, annualExpenses, rows: null);
    }

public static bool TryCalculateMaximumAnnualExpenses(Portfolio portfolio, out decimal maximumAnnualExpenses)
{
    maximumAnnualExpenses = 0m;

    if (portfolio.Accounts.Count == 0 || portfolio.CurrentAge > portfolio.LifeExpectancy)
    {
        return false;
    }

    decimal lowerBound = 0m;
    decimal upperBound = Math.Max(1m, portfolio.Accounts.Sum(a => a.Balance));

    if (ComputeFinalBalance(portfolio, lowerBound) < 0m)
    {
        return false;
    }

    bool foundUpperBound = ComputeFinalBalance(portfolio, upperBound) < 0m;

    for (int i = 0; i < 32 && !foundUpperBound; i++)
    {
        upperBound *= 2m;
        foundUpperBound = ComputeFinalBalance(portfolio, upperBound) < 0m;
    }

    if (!foundUpperBound)
    {
        return false;
    }

    const decimal tolerance = 0.01m;
    const int maxIterations = 100;

    for (int iteration = 0; iteration < maxIterations && (upperBound - lowerBound) > tolerance; iteration++)
    {
        decimal midpoint = (lowerBound + upperBound) / 2m;
        decimal finalBalance = ComputeFinalBalance(portfolio, midpoint);

        if (finalBalance >= 0m)
        {
            lowerBound = midpoint;
        }
        else
        {
            upperBound = midpoint;
        }
    }

    maximumAnnualExpenses = decimal.Round(lowerBound, 2, MidpointRounding.ToZero);
    return true;
}

    private static decimal ComputeFinalBalance(Portfolio portfolio, decimal annualExpenses, List<FinancialPlanRow>? rows)
    {
        var inflationRateDecimal = portfolio.AnnualInflationRate / 100m;
        var securitiesRateDecimal = portfolio.SecuritiesAnnualInterestRate / 100m;
        var currentExpenses = annualExpenses;
        var currentBalance = portfolio.Accounts.Sum(account => account.Balance) - currentExpenses;

        for (var year = portfolio.CurrentAge; year <= portfolio.LifeExpectancy; year++)
        {
            rows?.Add(new FinancialPlanRow(year, currentExpenses, currentBalance));

            currentExpenses *= 1m + inflationRateDecimal;
            currentBalance = (currentBalance * (1m + securitiesRateDecimal)) - currentExpenses;
        }

        return rows is { Count: > 0 } ? rows[^1].Balance : 0m;
    }
}

public sealed record FinancialPlanRow(int Year, decimal Expenses, decimal Balance);
