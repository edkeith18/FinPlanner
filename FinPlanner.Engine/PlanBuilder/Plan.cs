using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FinPlanner.Engine;

public class Plan
{

    /// <summary>
    /// The user's age at the start of the plan.
    /// </summary>
    public int StartAge { get; private set; }

    /// <summary>
    /// The user's age at the end of the plan.
    /// </summary>
    public int EndAge { get; private set; }

    /// <summary>
    /// The calendar year at the start of the plan.
    /// </summary>
    public int StartYear { get; private set; }

    /// <summary>
    /// The calendar year at the end of the plan.
    /// </summary>
    public int EndYear {  get; private set; }

    /// <summary>
    /// The accounts in the plan. Each account represents a financial account that is part of the user's financial plan.
    /// </summary>
    public List<Account> Accounts { get; private set; } = new List<Account>();

    /// <summary>
    /// Indicates whether or not plan was built successfully
    /// </summary>
    public bool IsSuccessful { get; private set; } = false;

    public string? FailureReason { get; private set; } = null;

    /// <summary>
    /// The PlanYears in the plan, in chronological order. Each PlanYear represents the financial results for a single 12-month period.
    /// </summary>
    public List<PlanYear> PlanYears { get; private set; } = new List<PlanYear>();

    private Plan(Scenario scenario)
    {

        // Store plan settings from the scenario
        StartAge = scenario.CurrentAge;
        EndAge = scenario.LifeExpectancy;

        StartYear = scenario.StartYear;
        EndYear = StartYear + (EndAge - scenario.CurrentAge);

        Accounts = scenario.Accounts.Select(account => new Account(account)).ToList();

    }

    public static Plan Build(Scenario scenario)
    {
        // Create the plan
        Plan plan = new Plan(scenario);

        // Calculate the first year of the plan
        var firstYear = PlanYear.CalculateFirst(
            plan.StartAge,
            plan.StartYear,
            scenario.Accounts);

        // Calculate the remaining years of the plan
        for (int year = plan.StartYear + 1; year <= plan.EndYear; year++)
        {
            var previousYear = plan.PlanYears[^1];

            var nextYear = PlanYear.CalculateNext(previousYear);

            plan.PlanYears.Add(nextYear);
        }

        return plan;
    }

}
