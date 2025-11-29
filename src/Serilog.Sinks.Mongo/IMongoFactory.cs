using MongoDB.Bson;
using MongoDB.Driver;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Factory interface for creating and managing MongoDB client, database, and collection instances.
/// </summary>
public interface IMongoFactory
{
    /// <summary>
    /// Gets or creates a MongoDB client instance based on the provided options.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing connection information.</param>
    /// <returns>A MongoDB client instance.</returns>
    ValueTask<IMongoClient> GetClient(MongoSinkOptions options);

    /// <summary>
    /// Gets or creates a MongoDB database instance based on the provided options.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing database configuration.</param>
    /// <param name="client">Optional MongoDB client instance to use. If <c>null</c>, a client will be created automatically.</param>
    /// <returns>A MongoDB database instance.</returns>
    ValueTask<IMongoDatabase> GetDatabase(MongoSinkOptions options, IMongoClient? client = null);

    /// <summary>
    /// Gets or creates a MongoDB collection instance based on the provided options.
    /// Ensures the collection exists and creates necessary indexes.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing collection configuration.</param>
    /// <param name="database">Optional MongoDB database instance to use. If <c>null</c>, a database will be created automatically.</param>
    /// <returns>A MongoDB collection for storing BSON documents.</returns>
    ValueTask<IMongoCollection<BsonDocument>> GetCollection(MongoSinkOptions options, IMongoDatabase? database = null);
}
