namespace FinPlanner.Engine;

/// <summary>
/// Ports the active 2026 VBA MFJ model and its projection assumptions.
/// This preserves the workbook's formulas, including the AMT expression,
/// and negative deduction behavior.
/// </summary>
public static class TaxCalculator
{
    public static TaxResult Calculate(PlanYear year, TaxInfo taxInfo)
    {
        decimal ordinary = 0m, divQ = 0m, divUQ = 0m, interest = 0m, ltcg = 0m, stcg = 0m;
        void Add(IncomeType type, decimal amount)
        {
            switch (type)
            {
                case IncomeType.Ordinary: ordinary += amount; break;
                case IncomeType.SocialSecurity: ordinary += amount * taxInfo.SocialSecurity.TaxablePercentage; break;
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
                    ltcg += transfer.RealizedGain ?? transfer.Amount;
                    break;
                case AccountType.Brokerage when source.Holdings == AccountHoldings.Bonds:
                    ordinary += transfer.RealizedGain ?? 0m;
                    break;
                // Return of basis, qualified HSA withdrawals, and Roth withdrawals are tax-free.
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
        healthcare = Math.Max(0m, healthcare - agi * taxInfo.HealthcareDeduction.AgiFloorPercentage);
        charity -= agi * taxInfo.CharityDeduction.AgiReductionPercentage;
        charity = Math.Min(charity, agi * taxInfo.CharityDeduction.AgiLimitPercentage);
        var saltMaximum = taxInfo.SaltDeduction.MaximumAmount;
        var saltThreshold = taxInfo.SaltDeduction.AgiPhaseoutThreshold;
        if (year.StartingYear >= taxInfo.SaltDeduction.LimitChangeYear) { saltMaximum = taxInfo.SaltDeduction.MaximumAmountAfterChange; saltThreshold = agi; }
        salt = Math.Min(salt, saltMaximum - taxInfo.SaltDeduction.PhaseoutPercentage * (agi - saltThreshold));
        var standardDeduction = taxInfo.StandardDeduction.Amount * Growth(taxInfo.StandardDeduction.AnnualRateOfIncrease, year.YearsInFuture);
        var deductions = Math.Max(standardDeduction, healthcare + charity + salt);
        // Preserve VBA: HSA reduces AGI, but is not subtracted again in this ordinary base.
        var taxableOrdinary = Math.Max(0m, ordinary + interest + divUQ + stcg - deductions);
        var taxableGains = ltcg + divQ;
        var bracketMultiplier = Growth(taxInfo.OrdinaryIncomeTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture);
        var ordinaryTax = BracketTax(taxableOrdinary,
            taxInfo.OrdinaryIncomeTax.Brackets.Reverse().Select(b => b.Threshold).ToArray(),
            taxInfo.OrdinaryIncomeTax.Brackets.Reverse().Select(b => b.Rate).ToArray(), bracketMultiplier);
        // Preserve VBA's order: subtract ordinary income before multiplying thresholds.
        var gainsTax = BracketTax(taxableGains,
            taxInfo.CapitalGainsTax.Brackets.Reverse().Select(b => Math.Max(0m, b.Threshold - taxableOrdinary)).ToArray(),
            taxInfo.CapitalGainsTax.Brackets.Reverse().Select(b => b.Rate).ToArray(),
            Growth(taxInfo.CapitalGainsTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture));
        var niit = taxInfo.NetInvestmentIncomeTax.Rate * Math.Max(0m, Math.Min(interest + divQ + divUQ + ltcg + stcg, agi - taxInfo.NetInvestmentIncomeTax.AgiThreshold));
        var amtBase = Math.Max(0m, agi - healthcare - charity - taxInfo.AlternativeMinimumTax.ExemptionAmount * Growth(taxInfo.AlternativeMinimumTax.ExemptionAnnualRateOfIncrease, year.YearsInFuture));
        var amtThreshold = taxInfo.AlternativeMinimumTax.Threshold * Growth(taxInfo.AlternativeMinimumTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture);
        // Literal VBA expression: the upper-tier component is multiplied by 26% again.
        var tentativeAmt = (Math.Max(0m, (amtBase - amtThreshold) * taxInfo.AlternativeMinimumTax.UpperRate)
            + Math.Min(amtBase, amtThreshold)) * taxInfo.AlternativeMinimumTax.LowerRate;
        var amt = Math.Max(0m, tentativeAmt - ordinaryTax - gainsTax);
        return new TaxResult
        {
            OrdinaryIncome = Money(ordinary), QualifiedDividends = Money(divQ), UnqualifiedDividends = Money(divUQ),
            Interest = Money(interest), LongTermCapitalGains = Money(ltcg), ShortTermCapitalGains = Money(stcg),
            HsaContribution = Money(hsa), HealthcareDeduction = Money(healthcare), CharityDeduction = Money(charity),
            SaltDeduction = Money(salt), StandardDeduction = Money(standardDeduction), AGI = Money(agi),
            Deductions = Money(deductions), TaxableOrdinaryIncome = Money(taxableOrdinary), TaxableCapitalGains = Money(taxableGains),
            OrdinaryTax = Money(ordinaryTax), LTCGTax = Money(gainsTax), NIIT = Money(niit), AMT = Money(amt)
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
