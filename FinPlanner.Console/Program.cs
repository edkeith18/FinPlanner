using System.CommandLine;
using System.Text.Json;
using FinPlanner.Engine;

var rootCommand = new RootCommand("FinPlanner command-line tools");

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

var savePathArgument = new Argument<FileInfo>("path")
{
    Description = "Scenario JSON file to save."
};

var saveCommand = new Command("save")
{
    Description = "Save a scenario to disk."
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

rootCommand.Subcommands.Add(loadCommand);
rootCommand.Subcommands.Add(saveCommand);

return rootCommand.Parse(args).Invoke();