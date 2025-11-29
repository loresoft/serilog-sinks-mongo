using MongoDB.Bson;

using Serilog.Events;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Factory interface for creating BSON documents from Serilog log events.
/// </summary>
public interface IDocumentFactory
{
    /// <summary>
    /// Creates a BSON document from a log event for storage in MongoDB.
    /// </summary>
    /// <param name="logEvent">The Serilog log event to convert.</param>
    /// <param name="options">The MongoDB sink options that control document creation.</param>
    /// <returns>A BSON document representation of the log event, or <c>null</c> if the log event should not be persisted.</returns>
    BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options);
}
