namespace FinPlanner.Engine;

/// <summary>
/// Ports the active 2026 VBA MFJ model and its projection assumptions.
/// This preserves the workbook's formulas, including the AMT expression,
/// negative deduction behavior, and estate loop that omits its lowest band.
/// </summary>
public static class TaxCalculator
{
    // With nonnegative deductions selected against the standard deduction,
    // ordinary tax is at most 37% and NIIT at most 3.8% of gross income.
    internal const decimal MaximumIncomeTaxRate = 0.408m;

    public static TaxResult Calculate(PlanYear year, int yearsInFuture)
    {
        ArgumentNullException.ThrowIfNull(year);
        ArgumentOutOfRangeException.ThrowIfNegative(yearsInFuture);
        decimal ordinary = 0m, divQ = 0m, divUQ = 0m, interest = 0m, ltcg = 0m, stcg = 0m;
        void Add(IncomeType type, decimal amount)
        {
            switch (type)
            {
                case IncomeType.Ordinary: ordinary += amount; break;
                case IncomeType.SocialSecurity: ordinary += amount * 0.85m; break;
                case IncomeType.DivQ: divQ += amount; break;
                case IncomeType.DivUQ: divUQ += amount; break;
                case IncomeType.Interest: interest += amount; break;
                case IncomeType.Ltcg: ltcg += amount; break;
                case IncomeType.Stcg: stcg += amount; break;
                case IncomeType.TaxFree: break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
        foreach (var earnings in year.AccountEarnings)
        {
            interest += earnings.Interest;
            divQ += earnings.DivQ;
            divUQ += earnings.DivUQ;
        }
        decimal hsa = 0m;
        foreach (var transfer in year.Transfers)
        {
            if (transfer.Reason == TransferReason.SurplusReinvestment) continue;
            if (transfer.Reason == TransferReason.ExternalIncome)
            {
                Add(transfer.TaxType, transfer.Amount);
                continue;
            }
            var source = transfer.FromAccount;
            if (source is null) continue;
            if (transfer.Reason == TransferReason.Scheduled && transfer.ToAccount?.Type == AccountType.Hsa
                && source.Type != AccountType.Hsa)
                hsa += transfer.Amount;
            switch (source.Type)
            {
                case AccountType.Retirement:
                case AccountType.Inherited:
                    if (transfer.ToAccount?.Type is AccountType.Retirement or AccountType.Inherited) break;
                    ordinary += transfer.Amount;
                    break;
                case AccountType.Brokerage when source.Holdings == AccountHoldings.Equities:
                    ltcg += transfer.Amount;
                    break;
                // Bond principal, qualified HSA withdrawals, and Roth withdrawals are tax-free.
            }
        }
        decimal healthcare = 0m, charity = 0m, salt = 0m;
        foreach (var expense in year.Expenses)
        {
            switch (GetExpenseType(expense))
            {
                case ExpenseType.Healthcare: healthcare += expense.Amount; break;
                case ExpenseType.Charity: charity += expense.Amount; break;
                case ExpenseType.Salt: salt += expense.Amount; break;
                case ExpenseType.HsaContribution: hsa += expense.Amount; break;
            }
        }
        var agi = ordinary - hsa + interest + divQ + divUQ + ltcg + stcg;
        healthcare = Math.Max(0m, healthcare - agi * 0.075m);
        if (year.StartingYear > 2025) charity -= agi * 0.005m;
        charity = Math.Min(charity, agi * 0.3m);
        var saltMaximum = 40400m * Growth(0.01m, yearsInFuture);
        var saltThreshold = 500000m * Growth(0.01m, yearsInFuture);
        if (year.StartingYear > 2029) { saltMaximum = 10000m; saltThreshold = agi; }
        salt = Math.Min(salt, saltMaximum - 0.3m * (agi - saltThreshold));
        var standardDeduction = 32200m * Growth(0.0633m, yearsInFuture);
        var deductions = Math.Max(standardDeduction, healthcare + charity + salt);
        // Preserve VBA: HSA reduces AGI, but is not subtracted again in this ordinary base.
        var taxableOrdinary = Math.Max(0m, ordinary + interest + divUQ + stcg - deductions);
        var taxableGains = ltcg + divQ;
        var bracketMultiplier = Growth(0.022m, yearsInFuture);
        var ordinaryTax = BracketTax(taxableOrdinary,
            [768700m, 512450m, 403550m, 211400m, 100800m, 24800m, 0m],
            [0.37m, 0.35m, 0.32m, 0.24m, 0.22m, 0.12m, 0.1m], bracketMultiplier);
        // Preserve VBA's order: subtract ordinary income before multiplying thresholds.
        var gainsTax = BracketTax(taxableGains,
            [Math.Max(0m, 613700m - taxableOrdinary), Math.Max(0m, 98900m - taxableOrdinary)],
            [0.2m, 0.15m], bracketMultiplier);
        var niit = 0.038m * Math.Max(0m, Math.Min(interest + divQ + divUQ + ltcg + stcg, agi - 250000m));
        var amtBase = Math.Max(0m, agi - healthcare - charity - 140200m * Growth(0.03487m, yearsInFuture));
        var amtThreshold = 249500m * Growth(0.04173m, yearsInFuture);
        // Literal VBA expression: the upper-tier component is multiplied by 26% again.
        var tentativeAmt = (Math.Max(0m, (amtBase - amtThreshold) * 0.28m)
            + Math.Min(amtBase, amtThreshold)) * 0.26m;
        var amt = Math.Max(0m, tentativeAmt - ordinaryTax - gainsTax);
        var estate = year.EndingBalance;
        var estateTax = BracketTax(Math.Max(0m, estate - 6000000m * Growth(0.0386m, yearsInFuture)),
            [9000000m, 7000000m, 6000000m, 4000000m, 3000000m, 2000000m, 1000000m],
            [0.35m, 0.3m, 0.26m, 0.23m, 0.19m, 0.17m, 0.15m]);
        return new TaxResult
        {
            OrdinaryIncome = Money(ordinary), QualifiedDividends = Money(divQ), UnqualifiedDividends = Money(divUQ),
            Interest = Money(interest), LongTermCapitalGains = Money(ltcg), ShortTermCapitalGains = Money(stcg),
            HsaContribution = Money(hsa), HealthcareDeduction = Money(healthcare), CharityDeduction = Money(charity),
            SaltDeduction = Money(salt), StandardDeduction = Money(standardDeduction), AGI = Money(agi),
            Deductions = Money(deductions), TaxableOrdinaryIncome = Money(taxableOrdinary), TaxableCapitalGains = Money(taxableGains),
            OrdinaryTax = Money(ordinaryTax), LTCGTax = Money(gainsTax), NIIT = Money(niit), AMT = Money(amt),
            EstateSize = estate, EstateTaxWA = Money(estateTax)
        };
    }

    private static decimal BracketTax(decimal amount, decimal[] thresholds, decimal[] rates, decimal multiplier = 1m)
    {
        decimal tax = 0m;
        for (var i = 0; i < thresholds.Length; i++)
        {
            var slice = Math.Max(0m, amount - thresholds[i] * multiplier);
            tax += slice * rates[i];
            amount -= slice;
        }
        return tax;
    }

    internal static decimal Growth(decimal rate, int years)
    {
        var factor = 1m;
        for (var i = 0; i < years; i++) factor *= 1m + rate;
        return factor;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 4, MidpointRounding.ToEven);
    internal static ExpenseType GetExpenseType(Expense expense)
    {
        if (expense.Type != ExpenseType.General) return expense.Type;
        var name = expense.Name.ToLowerInvariant();
        if (name.Contains("healthcare") || name.Contains("medicare")) return ExpenseType.Healthcare;
        if (name.Contains("charit")) return ExpenseType.Charity;
        if (name.Contains("salt") || name.Contains("state and local tax")) return ExpenseType.Salt;
        if (name.Contains("hsa") && name.Contains("contribution")) return ExpenseType.HsaContribution;
        return ExpenseType.General;
    }
}
