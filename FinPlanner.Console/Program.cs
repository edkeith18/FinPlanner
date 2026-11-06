using System.CommandLine;
using FinPlanner.Console;
using System.Text.Json;
using FinPlanner.Engine;

var rootCommand = new RootCommand("FinPlanner command line tools");

var scenarioPathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file."
};

// Build a plan and write its yearly account balances and expenses beside the scenario file.
var buildCommand = new Command("build")
{
    Description = "Build a financial plan from a scenario file."
};

// Add an option for the tax info file, which is optional and defaults to "TaxInfo.json" in the same directory as the scenario file.
var taxInfoOption = new Option<FileInfo?>("--tax-info")
{
    Description = "Tax settings JSON file (defaults to TaxInfo.json beside the scenario)."
};
buildCommand.Options.Add(taxInfoOption);
buildCommand.Arguments.Add(scenarioPathArgument);

// Set the action for the build command.
buildCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(scenarioPathArgument)!;
    var scenario = GetScenario(file);

    try
    {
        var taxFile = parseResult.GetValue(taxInfoOption)
            ?? new FileInfo(Path.Combine(file.DirectoryName!, "TaxInfo.json"));
        scenario.TaxInfo = GetTaxInfo(taxFile);

        //  Instantiate the plan calculator
        var calculator = new PlanCalculator();

            // Calculate the current trajectory
            var plan = calculator.CalculateCurrentTrajectory(scenario);

            // Calculate the maximum spend plan
            var maximumSpendPlan = calculator.CalculateMaximumSpend(scenario);

            // Calculate the perpetual spend plan
            var perpetualSpendPlan = calculator.CalculatePerpetualSpend(scenario);
            if (!perpetualSpendPlan.IsSuccessful)
                Console.WriteLine($"Perpetual plan: {perpetualSpendPlan.FailureReason}");

        var plans = new List<Plan> { plan };
        if (maximumSpendPlan.IsSuccessful)
        {
            plans.Add(maximumSpendPlan);
        }

        if (perpetualSpendPlan.IsSuccessful)
        {
            plans.Add(perpetualSpendPlan);
        }

        // Write the plan to an Excel workbook
        var outputPath = PlanXlsxExporter.Write(
            plans,
            file);

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to build plan: {ex.Message}");
        return 4;
    }

});

rootCommand.Subcommands.Add(buildCommand);

return rootCommand.Parse(args).Invoke();

static Scenario GetScenario(FileInfo file)
    => ReadJsonInput(file, "scenario", Scenario.Deserialize);

static TaxInfo GetTaxInfo(FileInfo file)
    => ReadJsonInput(file, "tax settings", TaxInfo.Deserialize);

static T ReadJsonInput<T>(FileInfo file, string description, Func<string, T> deserialize)
{
    try
    {
        var json = File.ReadAllText(file.FullName);
        return deserialize(json);
    }
    catch (FileNotFoundException)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        Environment.Exit(1);
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Invalid {description} file: {ex.Message}");
        Environment.Exit(2);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to read {description}: {ex.Message}");
        Environment.Exit(3);
    }
    // This line will never be reached, but the compiler requires a return statement.
    return default!;
}
