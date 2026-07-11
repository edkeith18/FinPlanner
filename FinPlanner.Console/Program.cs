using System.CommandLine;
using System.Text.Json;
using FinPlanner.Engine;

var rootCommand = new RootCommand("FinPlanner command-line tools");

//
// load
//

var loadPathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file to load."
};

var loadCommand = new Command("load")
{
    Description = "Load a scenario from disk."
};

loadCommand.Arguments.Add(loadPathArgument);

loadCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(loadPathArgument)!;

    try
    {
        var json = File.ReadAllText(file.FullName);
        var scenario = Scenario.Deserialize(json);

        Console.WriteLine($"Loaded '{file.FullName}'");
        Console.WriteLine($"Accounts : {scenario.Accounts.Count}");
        Console.WriteLine($"Income   : {scenario.Income.Count}");
        Console.WriteLine($"Expenses : {scenario.Expenses.Count}");
        Console.WriteLine($"Transfers: {scenario.Transfers.Count}");

        return 0;
    }
    catch (FileNotFoundException)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        return 1;
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Invalid scenario file: {ex.Message}");
        return 2;
    }
});

//
// save
//

var savePathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file to save."
};

var saveCommand = new Command("save")
{
    Description = "Save a new scenario to disk."
};

saveCommand.Arguments.Add(savePathArgument);

saveCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(savePathArgument)!;

    try
    {
        var scenario = new Scenario();

        File.WriteAllText(file.FullName, scenario.Serialize());

        Console.WriteLine($"Saved '{file.FullName}'");

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
});

//
// upgrade
//

var upgradePathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file to upgrade."
};

var upgradeCommand = new Command("upgrade")
{
    Description =
        "Load a scenario using the current schema and write it back to the same file."
};

upgradeCommand.Arguments.Add(upgradePathArgument);

upgradeCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(upgradePathArgument)!;

    try
    {
        var json = File.ReadAllText(file.FullName);
        var scenario = Scenario.Deserialize(json);

        var backupPath = file.FullName + ".bak";
        var temporaryPath = file.FullName + ".tmp";

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
    catch (FileNotFoundException)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        return 1;
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Invalid scenario file: {ex.Message}");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to upgrade scenario: {ex.Message}");
        return 3;
    }
});

rootCommand.Subcommands.Add(loadCommand);
rootCommand.Subcommands.Add(saveCommand);
rootCommand.Subcommands.Add(upgradeCommand);

return rootCommand.Parse(args).Invoke();