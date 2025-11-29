using MongoDB.Bson;

using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// A Serilog sink that writes log events to MongoDB in batches.
/// </summary>
/// <remarks>
/// This sink implements <see cref="IBatchedLogEventSink"/> to provide efficient batch processing of log events.
/// Log events are converted to BSON documents using an <see cref="IDocumentFactory"/> and written to MongoDB
/// using an <see cref="IMongoFactory"/> for connection management.
/// Errors during write operations are logged to <see cref="SelfLog"/> without throwing exceptions.
/// </remarks>
public class MongoSink : IBatchedLogEventSink
{
    private readonly MongoSinkOptions _options;
    private readonly IDocumentFactory _documentFactory;
    private readonly IMongoFactory _mongoFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoSink"/> class.
    /// </summary>
    /// <param name="options">The MongoDB sink options containing configuration settings.</param>
    /// <remarks>
    /// If <see cref="MongoSinkOptions.DocumentFactory"/> is not specified, a default <see cref="DocumentFactory"/> is used.
    /// If <see cref="MongoSinkOptions.MongoFactory"/> is not specified, a default <see cref="MongoFactory"/> is used.
    /// </remarks>
    public MongoSink(MongoSinkOptions options)
    {
        _options = options;
        _documentFactory = options.DocumentFactory ?? new DocumentFactory();
        _mongoFactory = options.MongoFactory ?? new MongoFactory();
    }

    /// <summary>
    /// Emits a batch of log events to MongoDB.
    /// </summary>
    /// <param name="batch">The collection of log events to write to MongoDB.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <remarks>
    /// This method:
    /// <list type="number">
    /// <item><description>Converts each log event to a BSON document using the configured <see cref="IDocumentFactory"/></description></item>
    /// <item><description>Filters out null documents (e.g., events below minimum log level)</description></item>
    /// <item><description>Writes all valid documents to MongoDB in a single batch operation</description></item>
    /// </list>
    /// If an error occurs during the write operation, it is logged to <see cref="SelfLog"/> and the exception is suppressed
    /// to prevent disrupting the application. Empty batches and batches with no valid documents are skipped.
    /// </remarks>
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

    /// <summary>
    /// Called when an empty batch is encountered during batching.
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// This method is part of the <see cref="IBatchedLogEventSink"/> interface contract.
    /// For the MongoDB sink, no action is required when an empty batch is encountered.
    /// </remarks>
    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}
