using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog.Sinks.Mongo.Sample.Services;
using Serilog.Sinks.SystemConsole.Themes;

namespace Serilog.Sinks.Mongo.Sample.Commands;

public class CappedCommand : Command
{
    private const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u1}] {Message:lj}{NewLine}{Exception}";

    public const string CommandName = "capped";
    public const string CommandDescription = "Use MongoDB Capped collection";

    public CappedCommand() : base(CommandName, CommandDescription)
    {
        SetAction(ExecuteAsync);
    }

    public static async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        // Configure Serilog with MongoDB Capped Collection
        builder.Services
            .AddSerilog(loggerConfiguration =>
            {
                loggerConfiguration
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: OutputTemplate,
                        theme: AnsiConsoleTheme.Code
                    )
                    .WriteTo.MongoDB(
                        connectionString: "mongodb://localhost:27017",
                        databaseName: "serilog",
                        collectionName: "logs-capped",
                        maxDocuments: 1000, // Max 1000 documents
                        maxSize: 5242880 // 5 MB max size
                    );
            });

        builder.Services
            .AddHostedService<LoggingService>();

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<CappedCommand>>();

        logger.LogInformation("Starting Capped Collection Sample...");
        logger.LogInformation("MongoDB Capped Collection: logs-capped");
        logger.LogInformation("Max Size: 5 MB, Max Documents: 1000");
        logger.LogInformation("Oldest logs will be automatically removed when limits are reached");
        logger.LogInformation("Press Ctrl+C to exit");

        await host.RunAsync(token: cancellationToken);

        return 0;
    }
}
