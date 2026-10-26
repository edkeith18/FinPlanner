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

buildCommand.Arguments.Add(scenarioPathArgument);

buildCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(scenarioPathArgument)!;
    var scenario = GetScenario(file);

    try
    {
        var calculator = new PlanCalculator();
        var plan = calculator.CalculateCurrentTrajectory(scenario);
        var maximumSpendPlan = calculator.CalculateMaximumSpend(scenario);
        var perpetualSpendPlan = calculator.CalculatePerpetualSpend(scenario);
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

// Rewrite a scenario using the current JSON schema while retaining a backup.

var upgradeCommand = new Command("upgrade")
{
    Description =
        "Load a scenario using the current schema and write it back to the same file."
};

upgradeCommand.Arguments.Add(scenarioPathArgument);

upgradeCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(scenarioPathArgument)!;
    var scenario = GetScenario(file);

    try
    {
        var backupPath = file.FullName + ".bak";
        var temporaryPath = file.FullName + ".tmp";

        // Write through a temporary file so the original remains intact until
        // the upgraded scenario has been serialized successfully.
        File.Copy(
            sourceFileName: file.FullName,
            destFileName: backupPath,
            overwrite: true);

        File.WriteAllText(
            temporaryPath,
            scenario.Serialize());

        File.Move(
            sourceFileName: temporaryPath,
            destFileName: file.FullName,
            overwrite: true);

        Console.WriteLine($"Upgraded '{file.FullName}'");
        Console.WriteLine($"Backup  : '{backupPath}'");

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to upgrade scenario: {ex.Message}");
        return 5;
    }
});

rootCommand.Subcommands.Add(buildCommand);
rootCommand.Subcommands.Add(upgradeCommand);

return rootCommand.Parse(args).Invoke();

static Scenario GetScenario(FileInfo file)
{
    try
    {
        var json = File.ReadAllText(file.FullName);
        return Scenario.Deserialize(json);
    }
    catch (FileNotFoundException)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        Environment.Exit(1);
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Invalid scenario file: {ex.Message}");
        Environment.Exit(2);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to read scenario: {ex.Message}");
        Environment.Exit(3);
    }
    // This line will never be reached, but the compiler requires a return statement.
    return null;
}
