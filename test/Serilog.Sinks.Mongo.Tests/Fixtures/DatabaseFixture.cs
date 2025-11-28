using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using MongoDB.Driver;

using Serilog.Events;

using Testcontainers.MongoDb;

namespace Serilog.Sinks.Mongo.Tests.Fixtures;

public class DatabaseFixture : TestApplicationFixture, IAsyncLifetime
{
    private const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u1}] {Message:lj}{NewLine}{Exception}";

    private readonly MongoDbContainer _mongoDbContainer = new MongoDbBuilder()
        .WithUsername(string.Empty)
        .WithPassword(string.Empty)
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _mongoDbContainer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _mongoDbContainer.DisposeAsync();
    }

    public string GetConnectionString() =>
        _mongoDbContainer.GetConnectionString();

    protected override void ConfigureApplication(HostApplicationBuilder builder)
    {
        base.ConfigureApplication(builder);

        // change database from container default
        var connectionBuilder = new MongoUrlBuilder(GetConnectionString())
        {
            DatabaseName = "MongoSinkTests"
        };

        // override connection string to use docker container
        var configurationData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SerilogMongo"] = connectionBuilder.ToString()
        };

        builder.Configuration.AddInMemoryCollection(configurationData);

        var services = builder.Services;

        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Debug)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
            .Enrich.WithProperty("EnvironmentName", builder.Environment.EnvironmentName)
            .Filter.ByExcluding(logEvent => logEvent.Exception is OperationCanceledException)
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.MongoDB(options =>
            {
                options.ConnectionString = builder.Configuration.GetConnectionString("SerilogMongo");
                options.DatabaseName = "MongoSinkTests";
                options.CollectionName = "logs";
                options.MinimumLevel = LogEventLevel.Debug;
            })
        );

    }
}
