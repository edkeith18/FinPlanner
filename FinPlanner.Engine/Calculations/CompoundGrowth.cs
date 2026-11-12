namespace FinPlanner.Engine;

internal static class CompoundGrowth
{
    /// <summary>Calculates an annual compound growth factor without rounding.</summary>
    internal static decimal Factor(decimal rate, int years)
    {
        var factor = 1m;
        for (var i = 0; i < years; i++) factor *= 1m + rate;
        return factor;
    }
}
