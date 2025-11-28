using MongoDB.Bson;

using Serilog.Events;

namespace Serilog.Sinks.Mongo;

public interface IDocumentFactory
{
    BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options);
}
