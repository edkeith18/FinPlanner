# VBA tax model

The engine ports the active formulas in `TaxCalculator`, `Account.AddIncome`, and the yearly tax-funding routines from the supplied `42YRPLAN.xlsm`. Tax parameters and projection growth assumptions come from `Input/TaxInfo.json`. Only MarriedFilingJointly is currently supported. It does not download tax tables or infer changes in legislation.

## Scenario inputs

The first year funds the supplied `FirstYearTaxExpense`; subsequent years calculate and fund taxes.

- `FirstYearAnnualExpenses`: the total first-year budget, including taxes and active named expenses.
- `FirstYearTaxExpense`: the known first-year tax amount (defaults to zero when omitted). This is fixed even when first-year withdrawals change.

- `Income.Type`: Ordinary (default), SocialSecurity, DivQ, DivUQ, Interest, Ltcg, Stcg, or TaxFree. An Ordinary income named Social Security uses SocialSecurity treatment for compatibility with the existing JSON.
- `Expense.Type`: General, Healthcare, Charity, Salt, or HsaContribution. General expenses named Healthcare/Medicare, Charitable giving, SALT/State and local taxes, or HSA Contribution are classified for compatibility with existing inputs. Uncategorized and Tax are reserved for generated annual results.
- Scheduled transfers identify their destination by `Name`, which must exactly match one existing account for each active instruction. JSON no longer contains source or destination account fields. After RMDs, each instruction draws from accounts in ascending `WithdrawalPriority`, excluding the destination and skipping empty accounts. Multiple source movements preserve individual tax treatment and cost basis. Only a shortfall across all eligible sources fails the transfer; unfunded amounts do not create deposits or HSA deductions.
- Actual scheduled transfers into an HSA from another account are deductible contributions. An explicit HsaContribution expense represents an additional contribution; do not record the same contribution in both places.

Brokerage accounts require explicit numeric `Basis` amounts (including zero) from scenario JSON and preserve them in scenario and yearly copies. Withdrawals (including scheduled cash transfers) remove proportional basis using the account balance immediately before the movement; only the positive difference between proceeds and removed basis is taxable. Equity gains are long-term capital gains; bond gains are ordinary income. Withdrawals below basis reduce basis proportionally but create no tax deduction or loss carryforward. Appreciation does not increase basis. Cash deposits and reinvested surplus add their full amounts to destination basis. Brokerage-to-brokerage movements are modeled as sales followed by cash purchases, not in-kind transfers. Both JSON readers reject brokerage accounts with missing or null basis; other account types may omit it. Executed transfers record their realized gains so later deposits or withdrawals cannot change the tax treatment of an earlier sale. Bond interest and ordinary retirement withdrawals and RMDs are fully taxable. Social Security uses the VBA's 85% inclusion assumption. Return of brokerage basis, Roth withdrawals, and qualified HSA withdrawals are tax-free. Retirement-to-retirement transfers are rollovers; scheduled traditional/inherited-to-Roth transfers are rejected because Roth conversions are not supported. Existing Roth balances, growth, and withdrawals remain supported. The current account model does not implement the VBA's separate real-estate, crypto, municipal-bond, or cash account classes. External income can explicitly carry capital-gain or tax-free treatment.

Account `WithdrawalPriority` values are preserved when loading, saving, and copying scenarios. Lower values fund voluntary withdrawals first, regardless of JSON account-array order; equal priorities retain array order. RMDs and scheduled transfers still run before voluntary expense funding. Newly added accounts receive a priority after the existing maximum. The UI displays loaded accounts by priority; dragging an account deliberately assigns priorities from the new display order.

## Annual funding

Initial Uncategorized Expenses equals `FirstYearAnnualExpenses - FirstYearTaxExpense - active named expenses`. A budget below the sum of first-year taxes and active named expenses is rejected. Both JSON readers reject negative numeric values anywhere in the input before deserializing, including nested and unknown fields. This validation does not inspect numeric strings and requires numbers to fit the decimal range. Future and expired named expenses do not reduce the initial budget.

The supplied tax amount is funded in the first year without calculating tax detail. `PlanYear.Taxes` is null for that year; `PlanYear.TaxExpense` contains the supplied amount. The first-year non-tax uncategorized amount is the baseline for later inflation adjustments. Maximum and perpetual spending searches vary the first-year total budget while keeping the supplied first-year tax amount fixed. `PlanYear.AnnualExpenses` reports each year's total modeled expenses; `Plan.AnnualExpenses` reports the selected initial budget.

Later years preserve that non-tax spending and withdraw additional funds to cover tax. Each trial resets balances and cost basis and removes trial expense withdrawals, then recalculates taxes from actual funded movements. Only a stable trial is retained. The tax expense is rounded to whole dollars, matching the plan's monetary forecast granularity. Tax component results retain four decimal places. A 100-iteration limit reports non-convergence rather than silently committing a partial trial.

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

The public perpetual-spending calculation uses `CalculatePerpetualSpend_Simple`: an upper/lower/midpoint search for the highest initial annual budget in $1,000 increments whose final forecast year grows total account balances by at least the scenario’s `AnnualInflationRate` (JSON percentage units). It compares the final ending balance with the previous year's ending balance, or the initial total balance for a one-year forecast. The comparison requires a positive starting total and does not round growth rates. Earlier years must fund all obligations but need not achieve inflation-matching growth. Taxes, income, transfers, and expenses are included in each trial's forecast. Overflow never counts as success. This finite-forecast target does not prove indefinite sustainability; the result's calculation note states that limitation.

## Reporting

Each annual sheet includes a Taxes expense. A separate tax-detail sheet identifies the supplied first-year amount without calculated detail. For subsequent years it exposes income classifications, AGI, deduction components, ordinary and capital-gains tax, AMT, NIIT, total tax, the rounded funded tax expense, and effective rate.

## Tax settings

The console `build` command loads `TaxInfo.json` beside the scenario file. Use `build <scenario-path> --tax-info <tax-settings-path>` to select another file. Missing or invalid settings stop the build with an error. Publishing also includes the supplied file under `Input/TaxInfo.json`.

The console loads the scenario and tax settings from separate JSON files, then assigns `scenario.TaxInfo = taxInfo`. Engine callers pass only the scenario to the stateless calculator: `new PlanCalculator().CalculateCurrentTrajectory(scenario)`. The maximum and perpetual methods use the same scenario property. Scenario snapshots preserve the immutable tax settings.

`Scenario.TaxInfo` has `[JsonIgnore]`, so scenario serialization and the `upgrade` command never embed tax settings or a tax file path. New and deserialized scenarios default to an embedded copy of the checked-in tax JSON, which also works in browser deployments without filesystem access. The embedded copy changes when the engine is rebuilt; the console replaces it with settings read from the external file on each build command.

All percentage values in tax JSON use percentage units (85 means 85%). Settings require every field, reject unknown properties and null sections, and validate nonnegative amounts, percentage ranges, ascending brackets, the supported filing status. Settings and bracket collections are immutable.

Projected tax growth uses `PlanYear.YearsInFuture`, the same zero-based offset used by forecasting. The supplied tax parameters apply at offset zero regardless of `StartingYear` or `TaxInfo.TaxYear`; `TaxYear` identifies the source tax data and does not determine growth. SALT's scheduled limit change still uses the calendar `StartingYear`. First-year supplied taxes remain authoritative. `TaxCalculator.Calculate(year, taxInfo)` reads `year.YearsInFuture` directly; there is no separate projection-offset argument.

Tax formulas, income classifications, RMD schedules, and account earnings assumptions remain implemented in code.
