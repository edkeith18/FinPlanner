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

    public static bool CalculateMaximumAnnualExpenses(Portfolio portfolio, out decimal maximumAnnualExpenses)
    {
        const decimal ExpenseStep = 1000m;

        maximumAnnualExpenses = 0m;

        if (portfolio.Accounts.Count == 0 || portfolio.CurrentAge > portfolio.LifeExpectancy)
        {
            return false;
        }

        int lowerBound = 0;
        int upperBound = Math.Max(1, (int)(portfolio.Accounts.Sum(a => a.Balance) / ExpenseStep));
        int potentialMaxium = 0;

        while (lowerBound <= upperBound)
        {
            int midPoint = (lowerBound + upperBound) / 2;
            decimal annualExpenses = midPoint * ExpenseStep;
            decimal finalBalance = ComputeFinalBalance(portfolio, annualExpenses, new List<FinancialPlanRow>());

            if (finalBalance >= 0m)
            {
                potentialMaxium = midPoint;
                lowerBound = midPoint + 1;
            }
            else
            {
                upperBound = midPoint - 1;
            }
        }

        maximumAnnualExpenses = potentialMaxium * ExpenseStep;
        return true;
    }

    private static decimal ComputeFinalBalance(Portfolio portfolio, decimal annualExpenses, List<FinancialPlanRow> rows)
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

        // The final balance after the last year of life expectancy is the last row's balance if rows were collected, otherwise it's just the computed balance. 
        return rows is { Count: > 0 } ? rows[^1].Balance : 0m;
    }
}

public sealed record FinancialPlanRow(int Year, decimal Expenses, decimal Balance);
