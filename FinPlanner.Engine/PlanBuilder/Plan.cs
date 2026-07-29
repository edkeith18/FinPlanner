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
    /// The calendar year at the start of the plan.
    /// </summary>
    public int StartYear { get; private set; }

    public int EndYear {  get; private set; }

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

        StartAge = scenario.CurrentAge;

        StartYear = scenario.StartYear;

    }

    public static Plan Build(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        // Create the plan
        Plan plan = new Plan(scenario);

        // Calculate and add PlanYears
        plan.PlanYears.Add(new PlanYear);

        return plan;
    }

}
