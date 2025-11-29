using MongoDB.Driver;

using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Configuration options for the MongoDB sink.
/// </summary>
public class MongoSinkOptions : BatchingOptions
{
    /// <summary>
    /// Gets or sets the minimum log event level to write to MongoDB.
    /// Log events below this level will be filtered out.
    /// </summary>
    /// <value>The minimum log level. Defaults to <see cref="LevelAlias.Minimum"/> (Verbose).</value>
    public LogEventLevel MinimumLevel { get; set; } = LevelAlias.Minimum;

    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    /// <value>The connection string used to connect to MongoDB. If not specified, defaults to localhost.</value>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the name of the MongoDB database to store log events.
    /// </summary>
    /// <value>The database name. Defaults to "serilog".</value>
    public string? DatabaseName { get; set; } = MongoSinkDefaults.DatabaseName;

    /// <summary>
    /// Gets or sets the name of the MongoDB collection to store log events.
    /// </summary>
    /// <value>The collection name. Defaults to "logs".</value>
    public string? CollectionName { get; set; } = MongoSinkDefaults.CollectionName;

    /// <summary>
    /// Gets or sets the MongoDB URL as an alternative to <see cref="ConnectionString"/>.
    /// If both are specified, <see cref="MongoUrl"/> takes precedence.
    /// </summary>
    /// <value>The MongoDB URL object.</value>
    public MongoUrl? MongoUrl { get; set; }

    /// <summary>
    /// Gets or sets the time-to-live (TTL) for log documents.
    /// When set, a TTL index is created on the Timestamp field to automatically expire and remove old documents.
    /// </summary>
    /// <value>The expiration timespan, or <c>null</c> for no automatic expiration.</value>
    /// <remarks>
    /// TTL indexes are not supported for capped collections.
    /// </remarks>
    public TimeSpan? ExpireAfter { get; set; }

    /// <summary>
    /// Gets or sets the set of property names to promote from the Properties object to the top level of the document.
    /// </summary>
    /// <value>A set of property names. Defaults to a set containing "SourceContext".</value>
    /// <remarks>
    /// Promoted properties appear as top-level fields in the MongoDB document rather than nested within the Properties object.
    /// </remarks>
    public HashSet<string>? Properties { get; set; } = [MongoSinkDefaults.SourceContext];

    /// <summary>
    /// Gets or sets the options for creating the MongoDB collection.
    /// </summary>
    /// <value>MongoDB collection creation options, such as capped collection settings or time-series options.</value>
    /// <remarks>
    /// Use this to configure capped collections, time-series collections, or other MongoDB collection features.
    /// </remarks>
    public CreateCollectionOptions? CollectionOptions { get; set; }

    /// <summary>
    /// Gets or sets the custom document factory used to convert log events to BSON documents.
    /// </summary>
    /// <value>A custom document factory implementation, or <c>null</c> to use the default factory.</value>
    /// <remarks>
    /// Implement <see cref="IDocumentFactory"/> to customize how log events are converted to MongoDB documents.
    /// </remarks>
    public IDocumentFactory? DocumentFactory { get; set; }

    /// <summary>
    /// Gets or sets the custom MongoDB factory used to create client, database, and collection instances.
    /// </summary>
    /// <value>A custom MongoDB factory implementation, or <c>null</c> to use the default factory.</value>
    /// <remarks>
    /// Implement <see cref="IMongoFactory"/> to customize MongoDB connection and resource management.
    /// </remarks>
    public IMongoFactory? MongoFactory { get; set; }
}
