namespace FinPlanner.Engine;

/// <summary>
/// Ports the active 2026 VBA MFJ model and its projection assumptions.
/// This preserves the workbook's formulas, including the AMT expression,
/// and negative deduction behavior.
/// </summary>
public static class TaxCalculator
{
    /// <summary>Calculates annual taxable income, deductions, and tax components from the period's earnings and funded transfers.</summary>
    public static TaxResult Calculate(PlanYear year, TaxInfo taxInfo)
    {
        decimal ordinary = 0m, divQ = 0m, divUQ = 0m, interest = 0m, ltcg = 0m, stcg = 0m;
        // Accumulates an income amount in the taxable category specified by its income type.
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

        // Accumulates income from the year's account earnings, which are already classified by type.
        foreach (var earnings in year.AccountEarnings)
        {
            interest += earnings.Interest;
            divQ += earnings.DivQ;
            divUQ += earnings.DivUQ;
        }

        // Accumulates income from the year's transfers, which are classified by source account type and transfer reason.
        decimal hsa = 0m;
        foreach (var transfer in year.Transfers)
        {
            // Skip reinvested surplus transfers, which are not taxable income.
            if (transfer.Reason == TransferReason.SurplusReinvestment) continue;

            // External income is already classified by type, so add it directly to the appropriate income category.
            if (transfer.Reason == TransferReason.ExternalIncome)
            {
                Add(transfer.TaxType, transfer.Amount);
                continue;
            }

            // Transfers from accounts are classified by the source account type, which determines the tax treatment of the transfer.
            var source = transfer.FromAccount;
            if (source is null) continue;

            // HSA contributions are tax-deductible, but only if they are made from outside the HSA. Transfers from an HSA to another account are not deductible.
            if (transfer.Reason == TransferReason.Scheduled && transfer.ToAccount?.Type == AccountType.Hsa
                && source.Type != AccountType.Hsa)
                hsa += transfer.Amount;

            // Transfers from retirement and inherited accounts are taxable unless they are transferred to another retirement or inherited account.
            // Transfers from brokerage accounts are taxable if they realize gains, which are classified by the source account's holdings.
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

        // Accumulates deductions from the year's expenses, which are classified by type.
        decimal healthcare = 0m, charity = 0m, salt = 0m;
        foreach (var expense in year.Expenses)
        {
            switch (expense.Type)
            {
                case ExpenseType.Healthcare: healthcare += expense.Amount; break;
                case ExpenseType.Charity: charity += expense.Amount; break;
                case ExpenseType.Salt: salt += expense.Amount; break;
                case ExpenseType.HsaContribution: hsa += expense.Amount; break;
            }
        }

        // Calculate AGI.  HSA reduces AGI directly (above-the-line deduction)
        var agi = ordinary - hsa + interest + divQ + divUQ + ltcg + stcg;

        // Calculate deductions

            // Healthcare deduction is reduced by AGI * floor percentage, but not below zero.
            healthcare = Math.Max(0m, healthcare - agi * taxInfo.HealthcareDeduction.AgiFloorPercentage);

            // Charity deduction is reduced by AGI * reduction percentage, but not below zero, and is capped at AGI * limit percentage.
            charity -= agi * taxInfo.CharityDeduction.AgiReductionPercentage;
            charity = Math.Min(charity, agi * taxInfo.CharityDeduction.AgiLimitPercentage);

            // SALT deduction is reduced by AGI * phaseout percentage, but not below zero, and is capped at the maximum deduction amount.
            var saltMaximum = taxInfo.SaltDeduction.MaximumAmount;
            var saltThreshold = taxInfo.SaltDeduction.AgiPhaseoutThreshold;
            if (year.StartingYear >= taxInfo.SaltDeduction.LimitChangeYear) { saltMaximum = taxInfo.SaltDeduction.MaximumAmountAfterChange; saltThreshold = agi; }
            salt = Math.Min(salt, saltMaximum - taxInfo.SaltDeduction.PhaseoutPercentage * (agi - saltThreshold));

            // Standard deduction is increased by its annual rate of increase, but not below zero.
            var standardDeduction = taxInfo.StandardDeduction.Amount * CompoundGrowth.Factor(taxInfo.StandardDeduction.AnnualRateOfIncrease, year.YearsInFuture);

            // Deductions are the maximum of the standard deduction and the sum of itemized deductions, which may be negative.
            var deductions = Math.Max(standardDeduction, healthcare + charity + salt);

        // Determine what is taxable as ordinary income
        var taxableOrdinary = Math.Max(0m, ordinary + interest + divUQ + stcg - deductions);

        // Determine what is taxable as capital gains
        var taxableGains = ltcg + divQ;

        // Calculate taxes
        // For all of these, the brackets and other thresholds need to be projected into the future, based on empirical growth rates

            // Calculate ordinary tax.
            var ordinaryTax = BracketTax(taxableOrdinary,
                taxInfo.OrdinaryIncomeTax.Brackets.Reverse().Select(b => b.Threshold).ToArray(),
                taxInfo.OrdinaryIncomeTax.Brackets.Reverse().Select(b => b.Rate).ToArray(), CompoundGrowth.Factor(taxInfo.OrdinaryIncomeTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture));
        
            // Calculate capital gains tax
            // Subtract ordinary income before multiplying thresholds.
            var gainsTax = BracketTax(taxableGains,
                taxInfo.CapitalGainsTax.Brackets.Reverse().Select(b => Math.Max(0m, b.Threshold - taxableOrdinary)).ToArray(),
                taxInfo.CapitalGainsTax.Brackets.Reverse().Select(b => b.Rate).ToArray(),
                CompoundGrowth.Factor(taxInfo.CapitalGainsTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture));

            // Calculate NIIT
            var niit = taxInfo.NetInvestmentIncomeTax.Rate * Math.Max(0m, Math.Min(interest + divQ + divUQ + ltcg + stcg, agi - taxInfo.NetInvestmentIncomeTax.AgiThreshold));

            // Calculate AMT
            var amtBase = Math.Max(0m, agi - healthcare - charity - taxInfo.AlternativeMinimumTax.ExemptionAmount * CompoundGrowth.Factor(taxInfo.AlternativeMinimumTax.ExemptionAnnualRateOfIncrease, year.YearsInFuture));
            var amtThreshold = taxInfo.AlternativeMinimumTax.Threshold * CompoundGrowth.Factor(taxInfo.AlternativeMinimumTax.ThresholdAnnualRateOfIncrease, year.YearsInFuture);

            var tentativeAmt = (Math.Max(0m, (amtBase - amtThreshold) * taxInfo.AlternativeMinimumTax.UpperRate)
                + Math.Min(amtBase, amtThreshold)) * taxInfo.AlternativeMinimumTax.LowerRate;
            var amt = Math.Max(0m, tentativeAmt - ordinaryTax - gainsTax);

        // Return the tax result
        return new TaxResult
        {
            OrdinaryIncome = ordinary,
            QualifiedDividends = divQ,
            UnqualifiedDividends = divUQ,
            Interest = interest,
            LongTermCapitalGains = ltcg,
            ShortTermCapitalGains = stcg,
            HsaContribution = hsa,
            HealthcareDeduction = healthcare,
            CharityDeduction = charity,
            SaltDeduction = salt,
            StandardDeduction = standardDeduction,
            AGI = agi,
            Deductions = deductions,
            TaxableOrdinaryIncome = taxableOrdinary,
            TaxableCapitalGains = taxableGains,
            OrdinaryTax = ordinaryTax,
            LTCGTax = gainsTax,
            NIIT = niit,
            AMT = amt
        };
    }

    /// <summary>Applies descending tax brackets to an income amount using the projected threshold multiplier.</summary>
    private static decimal BracketTax(decimal amount, decimal[] thresholds, decimal[] rates, decimal multiplier)
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

}
