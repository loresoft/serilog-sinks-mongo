using MongoDB.Bson;
using MongoDB.Driver;

using Serilog.Debugging;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Default implementation of <see cref="IMongoFactory"/> that creates and caches MongoDB client, database, and collection instances.
/// </summary>
/// <remarks>
/// This factory uses thread-safe lazy initialization to ensure single instances of the client, database, and collection
/// are created and reused across multiple log write operations. Collection existence and indexes are ensured on first access.
/// </remarks>
public class MongoFactory : IMongoFactory
{
#if NET8_0_OR_GREATER
    private readonly Lock _clientLock = new();
    private readonly Lock _databaseLock = new();
    private readonly Lock _collectionLock = new();
#else
    private readonly object _clientLock = new();
    private readonly object _databaseLock = new();
    private readonly object _collectionLock = new();
#endif

    private int _collectionInitialized = 0;

    private MongoClient? _mongoClient;
    private IMongoDatabase? _mongoDatabase;
    private IMongoCollection<BsonDocument>? _mongoCollection;

    /// <summary>
    /// Gets or creates a MongoDB client instance based on the provided options.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing connection information.</param>
    /// <returns>A cached MongoDB client instance.</returns>
    /// <remarks>
    /// The client is created on first call and cached for subsequent calls.
    /// Uses double-checked locking to ensure thread-safe singleton behavior.
    /// If neither <see cref="MongoSinkOptions.MongoUrl"/> nor <see cref="MongoSinkOptions.ConnectionString"/> 
    /// is specified, defaults to localhost:27017.
    /// </remarks>
    public ValueTask<IMongoClient> GetClient(MongoSinkOptions options)
    {
        if (_mongoClient != null)
            return new ValueTask<IMongoClient>(_mongoClient);

        lock (_clientLock)
        {
            if (_mongoClient != null)
                return new ValueTask<IMongoClient>(_mongoClient);

            var mongoUrl = options.MongoUrl ?? new MongoUrl(options.ConnectionString ?? MongoSinkDefaults.ConnectionString);

            _mongoClient = new MongoClient(mongoUrl);
            return new ValueTask<IMongoClient>(_mongoClient);
        }
    }

    /// <summary>
    /// Gets or creates a MongoDB database instance based on the provided options.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing database configuration.</param>
    /// <param name="client">Optional MongoDB client instance to use. If <c>null</c>, a client will be created automatically.</param>
    /// <returns>A cached MongoDB database instance.</returns>
    /// <remarks>
    /// The database is created on first call and cached for subsequent calls.
    /// Uses double-checked locking to ensure thread-safe singleton behavior.
    /// If <paramref name="client"/> is not provided, <see cref="GetClient"/> is called to obtain one.
    /// </remarks>
    public async ValueTask<IMongoDatabase> GetDatabase(MongoSinkOptions options, IMongoClient? client = null)
    {
        if (_mongoDatabase != null)
            return _mongoDatabase;

        client ??= await GetClient(options);

        lock (_databaseLock)
        {
            if (_mongoDatabase != null)
                return _mongoDatabase;

            var databaseName = options.DatabaseName ?? MongoSinkDefaults.DatabaseName;
            _mongoDatabase = client.GetDatabase(databaseName);
        }

        return _mongoDatabase;
    }

    /// <summary>
    /// Gets or creates a MongoDB collection instance based on the provided options.
    /// Ensures the collection exists and creates necessary indexes on first access.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing collection configuration.</param>
    /// <param name="database">Optional MongoDB database instance to use. If <c>null</c>, a database will be created automatically.</param>
    /// <returns>A cached MongoDB collection for storing BSON documents.</returns>
    /// <remarks>
    /// The collection is created on first call and cached for subsequent calls.
    /// Uses double-checked locking to ensure thread-safe singleton behavior.
    /// On first access, this method:
    /// <list type="bullet">
    /// <item><description>Ensures the collection exists (creates it if necessary using <see cref="MongoSinkOptions.CollectionOptions"/>)</description></item>
    /// <item><description>Creates indexes on Timestamp (descending, with optional TTL), Level, Message (text search), TraceId, and SpanId</description></item>
    /// </list>
    /// If <paramref name="database"/> is not provided, <see cref="GetDatabase"/> is called to obtain one.
    /// </remarks>
    public async ValueTask<IMongoCollection<BsonDocument>> GetCollection(MongoSinkOptions options, IMongoDatabase? database = null)
    {
        if (_mongoCollection != null)
            return _mongoCollection;

        database ??= await GetDatabase(options);

        lock (_collectionLock)
        {
            if (_mongoCollection != null)
                return _mongoCollection;

            var collectionName = options.CollectionName ?? MongoSinkDefaults.CollectionName;
            _mongoCollection = database.GetCollection<BsonDocument>(collectionName);
        }

        // Ensure collection exists and indexes are created only once
        if (Interlocked.Exchange(ref _collectionInitialized, 1) != 0)
            return _mongoCollection;

        await EnsureCollectionExists(options, database, _mongoCollection);
        await EnsureCollectionIndexes(options, _mongoCollection);

        return _mongoCollection;
    }

    /// <summary>
    /// Ensures the MongoDB collection exists, creating it if necessary.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing collection creation options.</param>
    /// <param name="database">The MongoDB database containing the collection.</param>
    /// <param name="collection">The collection to ensure exists.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the collection does not exist, it is created using the <see cref="MongoSinkOptions.CollectionOptions"/>.
    /// This supports creating capped collections, time-series collections, and other MongoDB collection types.
    /// </remarks>
    private static async Task EnsureCollectionExists(MongoSinkOptions options, IMongoDatabase database, IMongoCollection<BsonDocument> collection)
    {
        var collectionList = await database.ListCollectionNamesAsync();
        var collections = await collectionList.ToListAsync();

        if (collections.Contains(collection.CollectionNamespace.CollectionName))
            return;

        await database.CreateCollectionAsync(collection.CollectionNamespace.CollectionName, options.CollectionOptions);
    }

    /// <summary>
    /// Creates indexes on the MongoDB collection to optimize log event queries.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing index configuration.</param>
    /// <param name="mongoCollection">The collection to create indexes on.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Creates the following indexes:
    /// <list type="bullet">
    /// <item><description><b>Timestamp (descending)</b> - With optional TTL expiration (not supported for capped collections)</description></item>
    /// <item><description><b>Level (ascending)</b> - For filtering by log level</description></item>
    /// <item><description><b>Message (text)</b> - For full-text search (not supported for time-series collections)</description></item>
    /// <item><description><b>TraceId (ascending)</b> - For distributed tracing correlation</description></item>
    /// <item><description><b>SpanId (ascending)</b> - For distributed tracing correlation</description></item>
    /// </list>
    /// </remarks>
    private static async Task EnsureCollectionIndexes(MongoSinkOptions options, IMongoCollection<BsonDocument> mongoCollection)
    {
        var indexKeys = Builders<BsonDocument>.IndexKeys;

        var capped = options.CollectionOptions?.Capped == true;
        var timeSeries = options.CollectionOptions?.TimeSeriesOptions != null;

        // TTL index not supported for capped collections
        var expireAfter = capped ? null : options.ExpireAfter;

        var indexes = new List<CreateIndexModel<BsonDocument>>();

        var timeIndex = new CreateIndexModel<BsonDocument>(
            keys: indexKeys.Descending(MongoSinkDefaults.Timestamp),
            options: new CreateIndexOptions { ExpireAfter = expireAfter }
        );
        indexes.Add(timeIndex);

        var levelIndex = new CreateIndexModel<BsonDocument>(
            keys: indexKeys.Ascending(MongoSinkDefaults.Level)
        );
        indexes.Add(levelIndex);

        // text indexes not supported for time-series collections
        if (!timeSeries)
        {
            var messageIndex = new CreateIndexModel<BsonDocument>(
                keys: indexKeys.Text(MongoSinkDefaults.Message)
            );
            indexes.Add(messageIndex);
        }

        var traceIndex = new CreateIndexModel<BsonDocument>(
            keys: indexKeys.Ascending(MongoSinkDefaults.TraceId)
        );
        indexes.Add(traceIndex);

        var spanIndex = new CreateIndexModel<BsonDocument>(
            keys: indexKeys.Ascending(MongoSinkDefaults.SpanId)
        );
        indexes.Add(spanIndex);

        await mongoCollection.Indexes.CreateManyAsync(indexes);
    }
}
