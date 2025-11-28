using System.CommandLine;

using Serilog.Debugging;
using Serilog.Sinks.Mongo.Sample.Commands;

namespace Serilog.Sinks.Mongo.Sample;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        // Enable Serilog self-logging for diagnostics
        SelfLog.Enable(Console.Error);

        var rootCommand = new RootCommand("Serilog Mongo Sample Application");

        // Create 'timed' command for MongoDB Time Series Collection
        var timedCommand = new TimedCommand();
        rootCommand.Subcommands.Add(timedCommand);

        // Create 'capped' command for MongoDB Capped Collection
        var cappedCommand = new CappedCommand();
        rootCommand.Subcommands.Add(cappedCommand);

        // Create 'config' command for configuration-based setup
        var configCommand = new ConfigCommand();
        rootCommand.Subcommands.Add(configCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
