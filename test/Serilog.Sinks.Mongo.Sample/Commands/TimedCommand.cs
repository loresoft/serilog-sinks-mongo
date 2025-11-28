using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog.Sinks.Mongo.Sample.Services;
using Serilog.Sinks.SystemConsole.Themes;

namespace Serilog.Sinks.Mongo.Sample.Commands;

public class TimedCommand : Command
{
    private const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u1}] {Message:lj}{NewLine}{Exception}";

    public const string CommandName = "timed";
    public const string CommandDescription = "Use MongoDB TTL indexed collection";

    public TimedCommand() : base(CommandName, CommandDescription)
    {
        SetAction(ExecuteAsync);
    }

    public static async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        // Configure Serilog with MongoDB TTL indexed Collection
        builder.Services
            .AddSerilog(loggerConfiguration =>
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: OutputTemplate,
                        theme: AnsiConsoleTheme.Code
                    )
                    .WriteTo.MongoDB(
                        connectionString: "mongodb://localhost:27017",
                        databaseName: "serilog",
                        collectionName: "logs-ttl",
                        expireAfter: TimeSpan.FromDays(7) // Automatically delete logs after 7 days
                    );
            });

        builder.Services
            .AddHostedService<LoggingService>();

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<TimedCommand>>();

        logger.LogInformation("Starting Timed Collection Sample...");
        logger.LogInformation("MongoDB TTL indexed Collection: logs-ttl");
        logger.LogInformation("Logs will expire after 7 days");
        logger.LogInformation("Press Ctrl+C to exit");

        await host.RunAsync(token: cancellationToken);

        return 0;
    }
}
