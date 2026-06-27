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
            decimal finalBalance = ComputeFinalBalance(portfolio, annualExpenses);

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

    private static decimal ComputeFinalBalance(Portfolio portfolio, decimal annualExpenses, List<FinancialPlanRow>? rows = null)
    {
        var inflationRateDecimal = portfolio.AnnualInflationRate / 100m;
        var securitiesRateDecimal = portfolio.SecuritiesAnnualInterestRate / 100m;
        var copiedExpenses = CopyExpenses(portfolio.Expenses);
        var discretionaryExpenses = annualExpenses - copiedExpenses.Sum(expense => expense.Amount);
        var currentBalance = portfolio.Accounts.Sum(account => account.Balance);
        decimal finalBalance = 0m;

        for (var year = portfolio.CurrentAge; year <= portfolio.LifeExpectancy; year++)
        {
            if (year > portfolio.CurrentAge)
            {
                discretionaryExpenses *= 1m + inflationRateDecimal;

                foreach (var expense in copiedExpenses)
                {
                    var annualRateOfIncreaseDecimal = expense.AnnualRateOfIncrease / 100m;
                    expense.Amount *= 1m + annualRateOfIncreaseDecimal;
                }

                currentBalance *= 1m + securitiesRateDecimal;
            }

            var expenseBreakdowns = copiedExpenses
                .Select(expense => new FinancialPlanExpenseBreakdown(expense.Name, expense.Amount))
                .ToList();
            var totalExpenses = expenseBreakdowns.Sum(expense => expense.Amount) + discretionaryExpenses;
            finalBalance = currentBalance - totalExpenses;
            currentBalance = finalBalance;

            rows?.Add(new FinancialPlanRow(year, totalExpenses, expenseBreakdowns, discretionaryExpenses, finalBalance));
        }

        return finalBalance;
    }

    private static List<Expense> CopyExpenses(IEnumerable<Expense> expenses)
    {
        return expenses
            .Select(expense => new Expense
            {
                Name = expense.Name,
                Amount = expense.Amount,
                AgeStart = expense.AgeStart,
                AgeEnd = expense.AgeEnd,
                AnnualRateOfIncrease = expense.AnnualRateOfIncrease,
                UseInflationValue = expense.UseInflationValue,
                LastUpdated = expense.LastUpdated
            })
            .ToList();
    }
}

public sealed record FinancialPlanRow(
    int Year,
    decimal TotalExpenses,
    IReadOnlyList<FinancialPlanExpenseBreakdown> Expenses,
    decimal DiscretionaryExpenses,
    decimal Balance);

public sealed record FinancialPlanExpenseBreakdown(string Name, decimal Amount);
