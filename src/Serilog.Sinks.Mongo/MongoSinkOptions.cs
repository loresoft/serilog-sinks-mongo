using MongoDB.Driver;

using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Sinks.Mongo;

public class MongoSinkOptions : BatchingOptions
{
    public LogEventLevel MinimumLevel { get; set; } = LevelAlias.Minimum;

    public string? ConnectionString { get; set; }

    public string? DatabaseName { get; set; } = MongoSinkDefaults.DatabaseName;

    public string? CollectionName { get; set; } = MongoSinkDefaults.CollectionName;

    public MongoUrl? MongoUrl { get; set; }

    public TimeSpan? ExpireAfter { get; set; }

    public HashSet<string>? Properties { get; set; } = [MongoSinkDefaults.SourceContext];

    public CreateCollectionOptions? CollectionOptions { get; set; }

    public IDocumentFactory? DocumentFactory { get; set; }

    public IMongoFactory? MongoFactory { get; set; }
}
