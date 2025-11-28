using MongoDB.Bson;

using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

public class MongoSink : IBatchedLogEventSink
{
    private readonly MongoSinkOptions _options;
    private readonly IDocumentFactory _documentFactory;
    private readonly IMongoFactory _mongoFactory;

    public MongoSink(MongoSinkOptions options)
    {
        _options = options;
        _documentFactory = options.DocumentFactory ?? new DocumentFactory();
        _mongoFactory = options.MongoFactory ?? new MongoFactory();
    }

    public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
    {
        if (batch.Count == 0)
            return;

        try
        {
            // convert all events
            var documents = new List<BsonDocument>(batch.Count);
            foreach (var logEvent in batch)
            {
                var document = _documentFactory.CreateDocument(logEvent, _options);
                if (document != null)
                    documents.Add(document);
            }

            if (documents.Count == 0)
                return;

            var collection = await _mongoFactory.GetCollection(_options);
            await collection.InsertManyAsync(documents);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Error while emitting log events to MongoDB: {0}", ex.Message);
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}
