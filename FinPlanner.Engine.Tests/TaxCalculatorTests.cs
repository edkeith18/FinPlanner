using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

public class TaxCalculatorTests
{
    private static PlanYear IncomeYear(decimal ordinary = 0m, decimal divQ = 0m, int yearsInFuture = 0) => new()
    {
        StartingYear = 2026 + yearsInFuture,
        YearsInFuture = yearsInFuture,
        Transfers =
        [
            new AccountTransfer("Ordinary", null, null, ordinary, TransferReason.ExternalIncome),
            new AccountTransfer("Dividends", null, null, divQ, TransferReason.ExternalIncome) { TaxType = IncomeType.DivQ }
        ]
    };

    [Fact]
    public void OrdinaryIncomeUsesActiveVba2026BracketsAndStandardDeduction()
    {
        var taxes = TaxCalculator.Calculate(IncomeYear(100000m), TaxInfo.Default);
        Assert.Equal(100000m, taxes.AGI);
        Assert.Equal(32200m, taxes.Deductions);
        Assert.Equal(67800m, taxes.TaxableOrdinaryIncome);
        Assert.Equal(7640m, taxes.OrdinaryTax);
        Assert.Equal(7640m, taxes.TotalTax);
        Assert.Equal(0.0764m, taxes.EffectiveTaxRate);
    }

    [Fact]
    public void QualifiedDividendsStackOnOrdinaryIncomeAndPreserveUnusedDeductionBehavior()
    {
        Assert.Equal(165m, TaxCalculator.Calculate(IncomeYear(divQ: 100000m), TaxInfo.Default).LTCGTax);
        Assert.Equal(10335m, TaxCalculator.Calculate(IncomeYear(100000m, 100000m), TaxInfo.Default).LTCGTax);
    }

    [Fact]
    public void SocialSecurityIsEightyFivePercentTaxableOrdinaryIncome()
    {
        var year = IncomeYear();
        year.Transfers.Add(new AccountTransfer("Social Security", null, null, 100000m, TransferReason.ExternalIncome)
            { TaxType = IncomeType.SocialSecurity });
        var taxes = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(85000m, taxes.OrdinaryIncome);
        Assert.Equal(5840m, taxes.TotalTax);
    }

    [Fact]
    public void TransfersAreClassifiedBySourceWithTaxFreeRolloversAndRothWithdrawals()
    {
        var ira = new Account { Type = AccountType.Retirement };
        var equity = new Account { Type = AccountType.Brokerage };
        var bond = new Account { Type = AccountType.Brokerage, Holdings = AccountHoldings.Bonds };
        var roth = new Account { Type = AccountType.RetirementRoth };
        var year = new PlanYear
        {
            StartingYear = 2026,
            Transfers =
            [
                new AccountTransfer("RMD", ira, null, 10000m, TransferReason.Rmd),
                new AccountTransfer("Withdrawal", ira, null, 20000m, TransferReason.ExpenseWithdrawal),
                new AccountTransfer("Rollover", ira, new Account { Type = AccountType.Retirement }, 10000m, TransferReason.Scheduled),
                new AccountTransfer("Sale", equity, null, 100000m, TransferReason.ExpenseWithdrawal),
                new AccountTransfer("Principal", bond, null, 10000m, TransferReason.ExpenseWithdrawal),
                new AccountTransfer("Roth withdrawal", roth, null, 10000m, TransferReason.ExpenseWithdrawal)
            ]
        };
        var taxes = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(30000m, taxes.OrdinaryIncome);
        Assert.Equal(100000m, taxes.LongTermCapitalGains);
    }

    [Fact]
    public void BrokerageInterestAndUnqualifiedDividendsAreOrdinaryButQualifiedDividendsAreNot()
    {
        var account = new Account { Type = AccountType.Brokerage, BeginningBalance = 1000000m };
        var year = new PlanYear { StartingYear = 2026,
            AccountEarnings = [PlanYearCalculator.CalculateAccountEarnings(account)] };
        var taxes = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(1700m, taxes.Interest);
        Assert.Equal(9400m, taxes.UnqualifiedDividends);
        Assert.Equal(500m, taxes.QualifiedDividends);
        Assert.Equal(11600m, taxes.AGI);
    }

    [Fact]
    public void DeductionsPreserveHealthcareCharityAndSaltVbaExpressions()
    {
        var year = IncomeYear(100000m);
        year.Expenses =
        [
            new Expense { Name = "Healthcare", Amount = 20000m },
            new Expense { Name = "Charitable giving", Amount = 10000m },
            new Expense { Name = "State and local taxes (SALT)", Amount = 50000m }
        ];
        var taxes = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(12500m, taxes.HealthcareDeduction);
        Assert.Equal(9500m, taxes.CharityDeduction);
        // Literal VBA allows more than SALTMaximum below the AGI phaseout threshold.
        Assert.Equal(50000m, taxes.SaltDeduction);
        Assert.Equal(72000m, taxes.Deductions);
        Assert.Equal(2864m, taxes.OrdinaryTax);
        var high = TaxCalculator.Calculate(IncomeYear(1000000m), TaxInfo.Default);
        Assert.Equal(-5000m, high.CharityDeduction);
        Assert.Equal(-109600m, high.SaltDeduction);
        Assert.Equal(32200m, high.Deductions);
    }

    [Fact]
    public void HsaContributionsReduceAgiAndPreserveVbaOrdinaryBase()
    {
        var year = IncomeYear(100000m);
        year.Transfers.Add(new AccountTransfer("HSA contribution",
            new Account { Type = AccountType.Brokerage, Holdings = AccountHoldings.Bonds },
            new Account { Type = AccountType.Hsa }, 10000m, TransferReason.Scheduled));
        var taxes = TaxCalculator.Calculate(year, TaxInfo.Default);
        Assert.Equal(10000m, taxes.HsaContribution);
        Assert.Equal(90000m, taxes.AGI);
        Assert.Equal(67800m, taxes.TaxableOrdinaryIncome);
    }

    [Fact]
    public void NiitUsesInvestmentIncomeAndExcessAgiButNotRetirementIncomeAlone()
    {
        Assert.Equal(3800m, TaxCalculator.Calculate(IncomeYear(300000m, 100000m), TaxInfo.Default).NIIT);
        Assert.Equal(0m, TaxCalculator.Calculate(IncomeYear(300000m), TaxInfo.Default).NIIT);
    }

    [Fact]
    public void AmtPreservesTheLiteralVbaUpperTierExpression()
    {
        var taxes = TaxCalculator.Calculate(IncomeYear(divQ: 500000m), TaxInfo.Default);
        Assert.Equal(60165m, taxes.StandardTax);
        Assert.Equal(12916.84m, taxes.AMT);
        Assert.Equal(9500m, taxes.NIIT);
        Assert.Equal(82581.84m, taxes.TotalTax);
    }

    [Fact]
    public void ProjectedDeductionsAndSaltRulesUseTheirSeparateGrowthSchedules()
    {
        var year = IncomeYear(100000m, yearsInFuture: 1);
        year.Expenses.Add(new Expense { Type = ExpenseType.Salt, Amount = 50000m });
        Assert.Equal(34238.26m, TaxCalculator.Calculate(year, TaxInfo.Default).StandardDeduction);
        year = new PlanYear { StartingYear = 2030, YearsInFuture = 4, Expenses = [new Expense { Type = ExpenseType.Salt, Amount = 50000m }] };
        Assert.Equal(10000m, TaxCalculator.Calculate(year, TaxInfo.Default).SaltDeduction);
    }

}
