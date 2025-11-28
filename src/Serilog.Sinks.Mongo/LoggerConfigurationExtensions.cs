using MongoDB.Driver;

using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

public static class LoggerConfigurationExtensions
{
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

        return loggerConfiguration.Sink(sink, options, options.MinimumLevel);
    }

    public static LoggerConfiguration MongoDB(
        this LoggerSinkConfiguration loggerConfiguration,
        string connectionString,
        string databaseName = MongoSinkDefaults.DatabaseName,
        string collectionName = MongoSinkDefaults.CollectionName,
        LogEventLevel minimumLevel = LevelAlias.Minimum,
        TimeSpan? expireAfter = null,
        long? maxDocuments = null,
        long? maxSize = null,
        int? batchSizeLimit = null,
        TimeSpan? bufferingTimeLimit = null,
        IDocumentFactory? documentFactory = null)
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
            documentFactory
        );
    }

    public static LoggerConfiguration MongoDB(
        this LoggerSinkConfiguration loggerConfiguration,
        MongoUrl mongoUrl,
        string databaseName = MongoSinkDefaults.DatabaseName,
        string collectionName = MongoSinkDefaults.CollectionName,
        LogEventLevel minimumLevel = LevelAlias.Minimum,
        TimeSpan? expireAfter = null,
        long? maxDocuments = null,
        long? maxSize = null,
        int? batchSizeLimit = null,
        TimeSpan? bufferingTimeLimit = null,
        IDocumentFactory? documentFactory = null)
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

        return MongoDB(loggerConfiguration, sinkOptions =>
        {
            sinkOptions.MongoUrl = mongoUrl;
            sinkOptions.DatabaseName = databaseName;
            sinkOptions.CollectionName = collectionName;
            sinkOptions.MinimumLevel = minimumLevel;
            sinkOptions.CollectionOptions = collectionOptions;

            if (batchSizeLimit.HasValue)
                sinkOptions.BatchSizeLimit = batchSizeLimit.Value;

            if (bufferingTimeLimit.HasValue)
                sinkOptions.BufferingTimeLimit = bufferingTimeLimit.Value;

            if (documentFactory != null)
                sinkOptions.DocumentFactory = documentFactory;
        });
    }

}
