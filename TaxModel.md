# VBA tax model

The engine ports the active formulas in `TaxCalculator`, `Account.AddIncome`, and the yearly tax-funding routines from the supplied `42YRPLAN.xlsm`. It uses the workbook's 2026 married-filing-jointly constants and its assumed future growth rates. It does not download tax tables or infer changes in legislation.

## Scenario inputs

The first year funds the supplied `FirstYearTaxExpense`; subsequent years calculate and fund taxes.

- `FirstYearAnnualExpenses`: the total first-year budget, including taxes and active named expenses.
- `FirstYearTaxExpense`: the known first-year tax amount (defaults to zero when omitted). This is fixed even when first-year withdrawals change.

- `Income.Type`: Ordinary (default), SocialSecurity, DivQ, DivUQ, Interest, Ltcg, Stcg, or TaxFree. An Ordinary income named Social Security uses SocialSecurity treatment for compatibility with the existing JSON.
- `Expense.Type`: General, Healthcare, Charity, Salt, or HsaContribution. General expenses named Healthcare/Medicare, Charitable giving, SALT/State and local taxes, or HSA Contribution are classified for compatibility with existing inputs. Uncategorized and Tax are reserved for generated annual results.
- Actual scheduled transfers into an HSA from another account are deductible contributions. An explicit HsaContribution expense represents an additional contribution; do not record the same contribution in both places.

Brokerage equity withdrawals and transfers currently treat the full amount as long-term gains; cost basis is not tracked. Bond interest and ordinary retirement withdrawals and RMDs are fully taxable. Social Security uses the VBA's 85% inclusion assumption. Bond principal, Roth withdrawals, and qualified HSA withdrawals are tax-free. Retirement-to-retirement transfers are rollovers; scheduled traditional/inherited-to-Roth transfers are rejected because Roth conversions are not supported. Existing Roth balances, growth, and withdrawals remain supported. The current account model does not implement the VBA's separate real-estate, crypto, municipal-bond, or cash account classes. External income can explicitly carry capital-gain or tax-free treatment.

## Annual funding

Initial Uncategorized Expenses equals `FirstYearAnnualExpenses - FirstYearTaxExpense - active named expenses`. A budget below the sum of first-year taxes and active named expenses is rejected. Both JSON readers reject negative numeric values anywhere in the input before deserializing, including nested and unknown fields. This validation does not inspect numeric strings and requires numbers to fit the decimal range. Future and expired named expenses do not reduce the initial budget.

The supplied tax amount is funded in the first year without calculating tax detail. `PlanYear.Taxes` is null for that year; `PlanYear.TaxExpense` contains the supplied amount. The first-year non-tax uncategorized amount is the baseline for later inflation adjustments. Maximum and perpetual spending searches vary the first-year total budget while keeping the supplied first-year tax amount fixed. `PlanYear.AnnualExpenses` reports each year's total modeled expenses; `Plan.AnnualExpenses` reports the selected initial budget.

Later years preserve that non-tax spending and withdraw additional funds to cover tax. Each trial resets balances and removes trial expense withdrawals, then recalculates taxes from actual funded movements. Only a stable trial is retained. The tax expense is rounded to whole dollars, matching the plan's monetary forecast granularity. Tax component results retain four decimal places. A 100-iteration limit reports non-convergence rather than silently committing a partial trial.

Required distributions still occur. External income, account earnings, and RMD cash fund expenses before voluntary withdrawals. Surplus is reinvested only after the tax calculation converges. If the supplied first-year taxes plus active named expenses exceed the initial budget, the plan reports an invalid budget even if assets could fund a larger budget.

## Formulas deliberately retained for review

- Ordinary brackets: 10%, 12%, 22%, 24%, 32%, 35%, and 37%; thresholds grow 2.2% annually.
- Qualified dividends and long-term gains use the VBA's 0%/15%/20% stacking expression. Ordinary income is subtracted from the base threshold before the projected threshold multiplier is applied. Unused standard deduction does not reduce this capital-gains base.
- Standard deduction starts at $32,200 and grows 6.33% annually. Healthcare uses the 7.5%-of-AGI floor. Charity uses the 2026 0.5%-of-AGI reduction and 30%-of-AGI ceiling, without a zero floor.
- The SALT expression is reproduced literally, including its behavior below the phaseout threshold and possible negative result above it. Starting in 2030 its limit becomes $10,000.
- HSA contributions reduce AGI, but the VBA ordinary-income base does not separately subtract them.
- NIIT is 3.8% of the lesser of investment income or AGI above $250,000. The threshold is not indexed.
- AMT uses the workbook's exemption and threshold growth assumptions. Its upper-tier 28% component is multiplied by 26% again in the final expression; this is intentionally preserved.
- `MarginalTaxRate` in the VBA is total tax divided by AGI. The new result exposes EffectiveTaxRate and a documented MarginalTaxRate alias.

Decimal arithmetic replaces VBA Single values. This is a formula port, not a guarantee of bit-for-bit floating-point agreement.

## Perpetual spending

The tax-aware search simulates actual tax-funded years through scheduled transitions, then uses conservative bounds based on a 40.8% maximum income-tax envelope, including taxes on mandatory distributions and extra withdrawals. Future external income and deductions are conservatively ignored by that certificate. A successful result certifies a sustainable budget but does not prove the exact maximum; the console, result metadata, and XLSX tax sheet state this explicitly. A candidate without a certificate is not claimed to be insolvent. A horizon limit or numeric overflow never counts as a successful proof.

## Reporting

Each annual sheet includes a Taxes expense. A separate tax-detail sheet identifies the supplied first-year amount without calculated detail. For subsequent years it exposes income classifications, AGI, deduction components, ordinary and capital-gains tax, AMT, NIIT, total tax, the rounded funded tax expense, and effective rate.
