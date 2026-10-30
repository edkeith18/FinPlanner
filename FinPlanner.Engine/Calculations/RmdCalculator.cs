namespace FinPlanner.Engine;

/// <summary>
/// Planning withdrawals for rolling 12-month periods, using the period's starting age
/// and balance. Inherited divisors reproduce the personal FinPlanner V2 schedule.
/// </summary>
public static class RmdCalculator
{
    // Retirement accounts only: index 0 is age 75 (24.6), index 1 is age 76
    // (23.7), and each following index advances one year. Lookup index = age - 75.
    // Ages below 75 have no Retirement RMD; ages 120+ use the final divisor (2.0).
    // V2's Uniform Lifetime divisors through age 100, extended using IRS
    // Publication 590-B, Appendix B, Table III: https://www.irs.gov/publications/p590b
    // Inherited accounts use a separate personal schedule in GetDivisor:
    // age 52 starts at 34.3, declining by 1 each year to a minimum divisor of 1.
    private static readonly decimal[] RetirementDivisors =
    [
        24.6m, 23.7m, 22.9m, 22m, 21.1m, 20.2m, 19.4m, 18.5m, 17.7m,
        16.8m, 16m, 15.2m, 14.4m, 13.7m, 12.9m, 12.2m, 11.5m, 10.8m,
        10.1m, 9.5m, 8.9m, 8.4m, 7.8m, 7.3m, 6.8m, 6.4m, 6m, 5.6m,
        5.2m, 4.9m, 4.6m, 4.3m, 4.1m, 3.9m, 3.7m, 3.5m, 3.4m, 3.3m,
        3.1m, 3m, 2.9m, 2.8m, 2.7m, 2.5m, 2.3m, 2m
    ];

    /// <summary>
    /// Returns the RMD divisor for the account type and the 12-month period's starting age.
    /// </summary>
    /// <remarks>
    /// Retirement follows the IRS Uniform Lifetime Table (Publication 590-B,
    /// Appendix B, Table III), starting at age 75 with 24.6. Each subsequent age
    /// uses its table entry; these divisors do not simply decrease by 1 per year.
    /// Ages 120 and above use the table's final divisor of 2.0.
    /// Inherited follows the personal FinPlanner V2 schedule: start at 34.3 at
    /// age 52 and subtract 1 for each additional year, with a minimum divisor of 1.
    /// </remarks>
    /// <param name="type">The account type whose RMD schedule applies.</param>
    /// <param name="startingAge">PlanYear.StartingAge for the period being calculated.</param>
    /// <returns>
    /// The applicable divisor, or null before the type's starting age or for an
    /// account type without RMDs.
    /// </returns>
    public static decimal? GetDivisor(AccountType type, int startingAge) => type switch
    {
        AccountType.Retirement when startingAge >= 75 =>
            RetirementDivisors[Math.Min(startingAge, 120) - 75],
        AccountType.Inherited when startingAge >= 52 =>
            Math.Max(1m, 34.3m - ((decimal)startingAge - 52m)),
        _ => null
    };

    public static decimal Calculate(AccountType type, decimal startingBalance, int startingAge)
        => GetDivisor(type, startingAge) is decimal divisor
            ? decimal.Round(Math.Max(0m, startingBalance) / divisor,
                0, MidpointRounding.AwayFromZero)
            : 0m;

    internal static decimal GetMandatoryRate(Account account, int startingAge)
        => GetDivisor(account.Type, startingAge) is decimal divisor ? 1m / divisor : 0m;

    // Perpetual proofs must wait until future withdrawal rates stop changing.
    internal static bool IsScheduleStable(AccountType type, int startingAge) => type switch
    {
        AccountType.Retirement => startingAge >= 120,
        AccountType.Inherited => startingAge >= 86,
        _ => true
    };
}
