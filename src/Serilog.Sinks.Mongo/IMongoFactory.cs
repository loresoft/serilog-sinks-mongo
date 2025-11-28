using MongoDB.Bson;
using MongoDB.Driver;

namespace Serilog.Sinks.Mongo;

public interface IMongoFactory
{
    ValueTask<IMongoClient> GetClient(MongoSinkOptions options);

    ValueTask<IMongoDatabase> GetDatabase(MongoSinkOptions options, IMongoClient? client = null);

    ValueTask<IMongoCollection<BsonDocument>> GetCollection(MongoSinkOptions options, IMongoDatabase? database = null);
}
