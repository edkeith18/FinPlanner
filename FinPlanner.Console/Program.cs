using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FinPlanner.Engine;

var rootCommand = new RootCommand("FinPlanner command line tools");

var scenarioPathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file."
};

// Build a plan and write its yearly account balances beside the scenario file.

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
        // Create a plan from the scenario.
        var plan = new Plan(scenario);

        plan.Build();

        // Write the plan to a CSV file
        var outputPath = WritePlanCsv(
            plan,
            file);

        Console.WriteLine($"Maximum sustainable annual expenses: {plan.GetMaximumAnnualExpenses():C}");
        Console.WriteLine($"Plan written to '{outputPath}'");

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

static string WritePlanCsv(
    Plan plan,
    FileInfo scenarioFile)
{
    var timestamp = DateTime.Now.ToString(
        "yyyyMMdd_HHmmss",
        CultureInfo.InvariantCulture);
    var outputFileName =
        $"{Path.GetFileNameWithoutExtension(scenarioFile.Name)}_{timestamp}.csv";
    var outputPath = Path.Combine(
        scenarioFile.DirectoryName
            ?? Directory.GetCurrentDirectory(),
        outputFileName);

    var csv = new StringBuilder();
    var headers = new[] { "CalendarYear", "Age" }
        .Concat(plan.PlanYears.First().Accounts.Select(account => $"{account.Name} EndingBalance"));
    csv.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

    foreach (var planYear in plan.PlanYears)
    {
        var values = new List<string>
        {
            planYear.EndingYear.ToString(CultureInfo.InvariantCulture),
            planYear.EndingAge.ToString(CultureInfo.InvariantCulture)
        };

        // Output account balances
        // Precede values with a $ so that they render as currency in Excel. Use InvariantCulture to ensure that the decimal separator is a period, which Excel will interpret correctly regardless of the user's locale.
        values.AddRange(planYear.Accounts.Select(account =>
            $"${account.EndingBalance.ToString("0.00", CultureInfo.InvariantCulture)}"));

        csv.AppendLine(string.Join(",", values.Select(EscapeCsvField)));
    }
    File.WriteAllText(outputPath, csv.ToString());

    return outputPath;
}

static string EscapeCsvField(string value)
{
    if (!value.Contains(',')
        && !value.Contains('"')
        && !value.Contains('\r')
        && !value.Contains('\n'))
    {
        return value;
    }

    return $"\"{value.Replace("\"", "\"\"")}\"";
}
