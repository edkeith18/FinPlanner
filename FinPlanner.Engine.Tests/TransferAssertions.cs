using FinPlanner.Engine;

namespace FinPlanner.Engine.Tests;

internal static class TransferAssertions
{
    internal static decimal MandatoryFrom(this PlanYear year, int accountIndex)
        => year.Transfers.Where(transfer => transfer.FromAccount?.Id == year.Accounts[accountIndex].Id)
            .Sum(transfer => transfer.MandatoryAmount);

    internal static decimal VoluntaryFrom(this PlanYear year, int accountIndex)
        => year.Transfers.Where(transfer => transfer.FromAccount?.Id == year.Accounts[accountIndex].Id)
            .Sum(transfer => transfer.VoluntaryAmount);
}
