using MongoDB.Driver;

using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Provides extension methods for configuring the MongoDB sink with Serilog.
/// </summary>
public static class LoggerConfigurationExtensions
{
    /// <summary>
    /// Configures the logger to write log events to MongoDB using the specified configuration action.
    /// </summary>
    /// <param name="loggerConfiguration">The logger sink configuration.</param>
    /// <param name="configure">An action to configure the <see cref="MongoSinkOptions"/>.</param>
    /// <returns>The logger configuration for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="loggerConfiguration"/> or <paramref name="configure"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// Log.Logger = new LoggerConfiguration()
    ///     .WriteTo.MongoDB(options =>
    ///     {
    ///         options.ConnectionString = "mongodb://localhost:27017";
    ///         options.DatabaseName = "serilog";
    ///         options.CollectionName = "logs";
    ///         options.MinimumLevel = LogEventLevel.Information;
    ///         options.ExpireAfter = TimeSpan.FromDays(30);
    ///     })
    ///     .CreateLogger();
    /// </code>
    /// </example>
    public static LoggerConfiguration MongoDB(
        this LoggerSinkConfiguration loggerConfiguration,
        Action<MongoSinkOptions> configure)
    {
        if (loggerConfiguration is null)
            throw new ArgumentNullException(nameof(loggerConfiguration));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var options = new MongoSinkOptions();
        configure?.Invoke(options);

        var sink = new MongoSink(options);

        return loggerConfiguration.Sink(
            batchedLogEventSink: sink,
            batchingOptions: options,
            restrictedToMinimumLevel: options.MinimumLevel,
            levelSwitch: options.LevelSwitch);
    }

    /// <summary>
    /// Configures the logger to write log events to MongoDB using a connection string.
    /// </summary>
    /// <param name="loggerConfiguration">The logger sink configuration.</param>
    /// <param name="connectionString">The MongoDB connection string.</param>
    /// <param name="databaseName">The name of the MongoDB database. Defaults to "serilog".</param>
    /// <param name="collectionName">The name of the MongoDB collection. Defaults to "logs".</param>
    /// <param name="minimumLevel">The minimum log event level to write. Defaults to Verbose.</param>
    /// <param name="expireAfter">Optional time-to-live for log documents. When set, a TTL index is created.</param>
    /// <param name="maxDocuments">Optional maximum number of documents for a capped collection.</param>
    /// <param name="maxSize">Optional maximum size in bytes for a capped collection.</param>
    /// <param name="batchSizeLimit">Optional maximum number of events to include in a single batch.</param>
    /// <param name="bufferingTimeLimit">Optional maximum time to wait before writing a batch.</param>
    /// <param name="documentFactory">Optional custom document factory for converting log events to BSON.</param>
    /// <param name="levelSwitch">Optional logging level switch for dynamic log level control.</param>
    /// <param name="properties">Optional list of properties to include in the log documents.</param>
    /// <param name="optimizeProperties">If set to <c>true</c>, optimizes the storage of properties in the log documents.</param>
    /// <returns>The logger configuration for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loggerConfiguration"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is <c>null</c> or empty.</exception>
    /// <remarks>
    /// If <paramref name="maxSize"/> or <paramref name="maxDocuments"/> is specified, a capped collection will be created.
    /// TTL indexes are not supported for capped collections.
    /// </remarks>
    /// <example>
    /// <code>
    /// Log.Logger = new LoggerConfiguration()
    ///     .WriteTo.MongoDB(
    ///         connectionString: "mongodb://localhost:27017",
    ///         databaseName: "serilog",
    ///         collectionName: "logs",
    ///         minimumLevel: LogEventLevel.Information,
    ///         expireAfter: TimeSpan.FromDays(30)
    ///     )
    ///     .CreateLogger();
    /// </code>
    /// </example>
    public static LoggerConfiguration MongoDB(
        this LoggerSinkConfiguration loggerConfiguration,
        string connectionString,
        string? databaseName = null,
        string? collectionName = null,
        LogEventLevel minimumLevel = LevelAlias.Minimum,
        TimeSpan? expireAfter = null,
        long? maxDocuments = null,
        long? maxSize = null,
        int? batchSizeLimit = null,
        TimeSpan? bufferingTimeLimit = null,
        IDocumentFactory? documentFactory = null,
        LoggingLevelSwitch? levelSwitch = null,
        string[]? properties = null,
        bool optimizeProperties = false)
    {
        if (loggerConfiguration is null)
            throw new ArgumentNullException(nameof(loggerConfiguration));

        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or empty.", nameof(connectionString));

        var mongoUrl = new MongoUrl(connectionString);

        return MongoDB(
            loggerConfiguration,
            mongoUrl,
            databaseName,
            collectionName,
            minimumLevel,
            expireAfter,
            maxDocuments,
            maxSize,
            batchSizeLimit,
            bufferingTimeLimit,
            documentFactory,
            levelSwitch,
            properties,
            optimizeProperties
        );
    }

    /// <summary>
    /// Configures the logger to write log events to MongoDB using a <see cref="MongoUrl"/>.
    /// </summary>
    /// <param name="loggerConfiguration">The logger sink configuration.</param>
    /// <param name="mongoUrl">The MongoDB URL containing connection settings.</param>
    /// <param name="databaseName">The name of the MongoDB database. Defaults to "serilog".</param>
    /// <param name="collectionName">The name of the MongoDB collection. Defaults to "logs".</param>
    /// <param name="minimumLevel">The minimum log event level to write. Defaults to Verbose.</param>
    /// <param name="expireAfter">Optional time-to-live for log documents. When set, a TTL index is created.</param>
    /// <param name="maxDocuments">Optional maximum number of documents for a capped collection.</param>
    /// <param name="maxSize">Optional maximum size in bytes for a capped collection.</param>
    /// <param name="batchSizeLimit">Optional maximum number of events to include in a single batch.</param>
    /// <param name="bufferingTimeLimit">Optional maximum time to wait before writing a batch.</param>
    /// <param name="documentFactory">Optional custom document factory for converting log events to BSON.</param>
    /// <param name="levelSwitch">Optional logging level switch for dynamic log level control.</param>
    /// <param name="properties">Optional list of properties to include in the log documents.</param>
    /// <param name="optimizeProperties">If set to <c>true</c>, optimizes the storage of properties in the log documents.</param>
    /// <returns>The logger configuration for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="loggerConfiguration"/> or <paramref name="mongoUrl"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// If <paramref name="maxSize"/> or <paramref name="maxDocuments"/> is specified, a capped collection will be created.
    /// TTL indexes are not supported for capped collections.
    /// </remarks>
    /// <example>
    /// <code>
    /// var mongoUrl = new MongoUrl("mongodb://localhost:27017");
    /// Log.Logger = new LoggerConfiguration()
    ///     .WriteTo.MongoDB(
    ///         mongoUrl: mongoUrl,
    ///         databaseName: "serilog",
    ///         collectionName: "logs",
    ///         expireAfter: TimeSpan.FromDays(7)
    ///     )
    ///     .CreateLogger();
    /// </code>
    /// </example>
    public static LoggerConfiguration MongoDB(
        this LoggerSinkConfiguration loggerConfiguration,
        MongoUrl mongoUrl,
        string? databaseName = null,
        string? collectionName = null,
        LogEventLevel minimumLevel = LevelAlias.Minimum,
        TimeSpan? expireAfter = null,
        long? maxDocuments = null,
        long? maxSize = null,
        int? batchSizeLimit = null,
        TimeSpan? bufferingTimeLimit = null,
        IDocumentFactory? documentFactory = null,
        LoggingLevelSwitch? levelSwitch = null,
        string[]? properties = null,
        bool optimizeProperties = false)
    {
        if (loggerConfiguration is null)
            throw new ArgumentNullException(nameof(loggerConfiguration));

        if (mongoUrl is null)
            throw new ArgumentNullException(nameof(mongoUrl));

        var collectionOptions = new CreateCollectionOptions
        {
            Capped = maxSize.HasValue || maxDocuments.HasValue,
            MaxSize = maxSize,
            MaxDocuments = maxDocuments
        };

        // default database name from mongoUrl if not provided
        databaseName ??= mongoUrl.DatabaseName ?? MongoSinkDefaults.DatabaseName;

        return MongoDB(loggerConfiguration, sinkOptions =>
        {
            sinkOptions.MongoUrl = mongoUrl;
            sinkOptions.DatabaseName = databaseName;
            sinkOptions.CollectionName = collectionName;
            sinkOptions.MinimumLevel = minimumLevel;
            sinkOptions.CollectionOptions = collectionOptions;
            sinkOptions.LevelSwitch = levelSwitch;
            sinkOptions.ExpireAfter = expireAfter;
            sinkOptions.OptimizeProperties = optimizeProperties;

            if (properties != null)
                sinkOptions.Properties = [.. properties];

            if (batchSizeLimit.HasValue)
                sinkOptions.BatchSizeLimit = batchSizeLimit.Value;

            if (bufferingTimeLimit.HasValue)
                sinkOptions.BufferingTimeLimit = bufferingTimeLimit.Value;

            if (documentFactory != null)
                sinkOptions.DocumentFactory = documentFactory;
        });
    }

}
