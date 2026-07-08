namespace FinPlanner.Engine;

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
        int potentialMaximum = 0;
        decimal finalBalance = 0m;

        while (lowerBound <= upperBound)
        {
            int midPoint = (lowerBound + upperBound) / 2;
            decimal annualExpenses = midPoint * ExpenseStep;

            finalBalance = ComputeFinalBalance(portfolio, annualExpenses);

            if (finalBalance >= 0m)
            {
                potentialMaximum = midPoint;
                lowerBound = midPoint + 1;
            }
            else
            {
                upperBound = midPoint - 1;
            }
        }

        // The plan fails if the final balance is zero
        if (finalBalance <= 0m)
        {
            return false;
        }
        else
        {
            maximumAnnualExpenses = potentialMaximum * ExpenseStep;
            return true;
        }

    }

    private static decimal ComputeFinalBalance(Portfolio portfolio, decimal annualExpenses, List<FinancialPlanRow>? rows = null)
    {
        var inflationRateDecimal = portfolio.AnnualInflationRate / 100m;
        var securitiesRateDecimal = portfolio.SecuritiesAnnualInterestRate / 100m;
        var copiedExpenses = CopyExpenses(portfolio.Expenses);
        var discretionaryExpenses = 0m;
        var yearEndBalance = portfolio.Accounts.Sum(account => account.Balance);

        // Calculate discretionary expenses based on the first year
        // The discretionary expenses will increase with inflation each subsequent year
        discretionaryExpenses = annualExpenses - copiedExpenses.Sum(expense => IsExpenseActive(expense, portfolio.CurrentAge) ? expense.Amount : 0m);
        if (discretionaryExpenses < 0m)
        {
            // If discretionary expenses are negative, it means the specified annual expenses are less than the sum of active expenses
            // In this case, we can set discretionary expenses to zero and adjust the total expenses accordingly
            discretionaryExpenses = 0m;
        }

        // Now iterate through each year, updating expenses and balance accordingly and displaying the results in a table format
        for (var age = portfolio.CurrentAge; age <= portfolio.LifeExpectancy; age++)
        {

            // Only increment expenses after first year
            if (age > portfolio.CurrentAge)
            {

                foreach (var expense in copiedExpenses)
                    IncrementExpense(expense, age);

                // discretionary expenses are not part of the expenses list, so we need to manually increase them by the inflation rate each year
                discretionaryExpenses *= 1m + inflationRateDecimal;

                // Increment the year end balance
                yearEndBalance *= 1m + securitiesRateDecimal;

            }

            // Determine the total expenses for the current year, including discretionary expenses
            var expenseBreakdowns = copiedExpenses
                .Select(expense => new FinancialPlanExpenseBreakdown(
                    expense.Name,
                    IsExpenseActive(expense, age) ? expense.Amount : 0m))
                .ToList();
            var totalExpenses = expenseBreakdowns.Sum(expense => expense.Amount) + discretionaryExpenses;

            // Detemine the year end balance
            yearEndBalance -= totalExpenses;

            rows?.Add(new FinancialPlanRow(age, totalExpenses, expenseBreakdowns, discretionaryExpenses, yearEndBalance));
        }

        return yearEndBalance;
    }

    private static bool IsExpenseActive(Expense expense, int age)
    {
        return expense.AgeStart <= age && expense.AgeEnd >= age;
    }


    // Only increment expenses that are active and have an AgeStart greater than the current age

    private static void IncrementExpense(Expense expense, int age)
    {

        if (IsExpenseActive(expense, age) && expense.AgeStart < age)
        {

            var annualRateOfIncreaseDecimal = expense.AnnualRateOfIncrease / 100m;
            expense.Amount *= 1m + annualRateOfIncreaseDecimal;

        }

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
    int Age,
    decimal TotalExpenses,
    IReadOnlyList<FinancialPlanExpenseBreakdown> Expenses,
    decimal DiscretionaryExpenses,
    decimal Balance);

public sealed record FinancialPlanExpenseBreakdown(string Name, decimal Amount);
