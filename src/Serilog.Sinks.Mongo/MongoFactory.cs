using MongoDB.Bson;
using MongoDB.Driver;

using Serilog.Debugging;

namespace Serilog.Sinks.Mongo;

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

    private static async Task EnsureCollectionExists(MongoSinkOptions options, IMongoDatabase database, IMongoCollection<BsonDocument> collection)
    {
        var collectionList = await database.ListCollectionNamesAsync();
        var collections = await collectionList.ToListAsync();

        if (collections.Contains(collection.CollectionNamespace.CollectionName))
            return;

        await database.CreateCollectionAsync(collection.CollectionNamespace.CollectionName, options.CollectionOptions);
    }

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
