using System.Diagnostics;

using MongoDB.Bson;
using MongoDB.Driver;

using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Mongo.Tests.Fixtures;

namespace Serilog.Sinks.Mongo.Tests;

public class MongoSinkTests(DatabaseFixture databaseFixture) : DatabaseTestBase(databaseFixture)
{
    [Fact]
    public async Task EmitBatchAsync_WithSingleLogEvent_ShouldInsertDocument()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test message"
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(1);

        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);
        documents[0]["Message"].AsString.Should().Be("Test message");
        documents[0]["Level"].AsString.Should().Be("Information");
    }

    [Fact]
    public async Task EmitBatchAsync_WithMultipleLogEvents_ShouldInsertAllDocuments()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var logEvents = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, "Debug message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Info message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, "Warning message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, "Error message")
        };

        // Act
        await sink.EmitBatchAsync(logEvents);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(4);

        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);
        documents.Select(d => d["Level"].AsString).Should().Contain(["Debug", "Information", "Warning", "Error"]);
    }

    [Fact]
    public async Task EmitBatchAsync_WithEmptyBatch_ShouldNotThrow()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var emptyBatch = Array.Empty<LogEvent>();

        // Act & Assert
        await sink.EmitBatchAsync(emptyBatch);

        // Verify no documents were inserted
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(0);
    }

    [Fact]
    public async Task EmitBatchAsync_WithLogEventWithProperties_ShouldIncludeProperties()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue(123),
            ["UserName"] = new ScalarValue("John Doe")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "User logged in",
            properties: properties
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(1);
        var doc = documents[0];
        doc["Properties"]["UserId"].AsInt32.Should().Be(123);
        doc["Properties"]["UserName"].AsString.Should().Be("John Doe");
    }

    [Fact]
    public async Task EmitBatchAsync_WithLogEventWithException_ShouldIncludeException()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var exception = new InvalidOperationException("Something went wrong");
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "An error occurred",
            exception: exception
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(1);
        var doc = documents[0];
        doc.Contains("Exception").Should().BeTrue();
        doc["Exception"]["Message"].AsString.Should().Be("Something went wrong");
        doc["Exception"]["Type"].AsString.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task EmitBatchAsync_WithLogEventWithTraceId_ShouldIncludeTraceId()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Traced message",
            traceId: traceId,
            spanId: spanId
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(1);
        var doc = documents[0];
        doc["TraceId"].AsString.Should().Be(traceId.ToHexString());
        doc["SpanId"].AsString.Should().Be(spanId.ToHexString());
    }

    [Fact]
    public async Task EmitBatchAsync_WithCustomDocumentFactory_ShouldUseCustomFactory()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var customFactory = new CustomDocumentFactory();
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName,
            DocumentFactory = customFactory
        };

        var sink = new MongoSink(options);
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Custom factory test"
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        customFactory.CreateDocumentCallCount.Should().Be(1);

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(1);
        documents[0]["CustomField"].AsString.Should().Be("CustomValue");
    }

    [Fact]
    public async Task EmitBatchAsync_WithCustomMongoFactory_ShouldUseCustomFactory()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var customMongoFactory = new CustomMongoFactory(Fixture.GetConnectionString());
        var options = new MongoSinkOptions
        {
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName,
            MongoFactory = customMongoFactory
        };

        var sink = new MongoSink(options);
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Custom mongo factory test"
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        customMongoFactory.GetCollectionCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EmitBatchAsync_WithDocumentFactoryReturningNull_ShouldSkipNullDocuments()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var filteringFactory = new FilteringDocumentFactory();
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName,
            DocumentFactory = filteringFactory
        };

        var sink = new MongoSink(options);
        var logEvents = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, "Debug message"), // Will be filtered
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Info message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, "Warning message")
        };

        // Act
        await sink.EmitBatchAsync(logEvents);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(2); // Only Info and Warning should be inserted

        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);
        documents.Select(d => d["Level"].AsString).Should().NotContain("Debug");
    }

    [Fact]
    public async Task EmitBatchAsync_WithMinimumLevel_ShouldFilterLogEventsBelowMinimum()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName,
            MinimumLevel = LogEventLevel.Warning
        };

        var sink = new MongoSink(options);
        var logEvents = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Verbose, "Verbose message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, "Debug message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Info message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, "Warning message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, "Error message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, "Fatal message")
        };

        // Act
        await sink.EmitBatchAsync(logEvents);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);
        
        // Only Warning, Error, and Fatal should be inserted
        documents.Should().HaveCount(3);
        documents.Select(d => d["Level"].AsString).Should().Contain(["Warning", "Error", "Fatal"]);
        documents.Select(d => d["Level"].AsString).Should().NotContain(["Verbose", "Debug", "Information"]);
    }

    [Fact]
    public async Task EmitBatchAsync_WithMinimumLevelAndAllEventsBelow_ShouldInsertNoDocuments()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName,
            MinimumLevel = LogEventLevel.Fatal
        };

        var sink = new MongoSink(options);
        var logEvents = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Verbose, "Verbose message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Debug, "Debug message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Info message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Warning, "Warning message"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, "Error message")
        };

        // Act
        await sink.EmitBatchAsync(logEvents);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(0);
    }

    [Fact]
    public async Task EmitBatchAsync_MultipleBatches_ShouldInsertAllDocuments()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);

        var batch1 = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Batch 1 - Message 1"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Batch 1 - Message 2")
        };

        var batch2 = new[]
        {
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Batch 2 - Message 1"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Batch 2 - Message 2"),
            CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Batch 2 - Message 3")
        };

        // Act
        await sink.EmitBatchAsync(batch1);
        await sink.EmitBatchAsync(batch2);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(5);
    }

    [Fact]
    public async Task EmitBatchAsync_WithLargeBatch_ShouldInsertAllDocuments()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var largeBatch = Enumerable.Range(1, 100)
            .Select(i => CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, $"Message {i}"))
            .ToArray();

        // Act
        await sink.EmitBatchAsync(largeBatch);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(100);
    }

    [Fact]
    public async Task EmitBatchAsync_WithComplexLogEvents_ShouldPreserveAllData()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);

        var nestedStructure = new StructureValue([
            new LogEventProperty("InnerProp", new ScalarValue("inner value"))
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue(12345),
            ["UserName"] = new ScalarValue("John Doe"),
            ["Timestamp"] = new ScalarValue(DateTime.UtcNow),
            ["IsActive"] = new ScalarValue(true),
            ["Score"] = new ScalarValue(98.5),
            ["Tags"] = new SequenceValue([new ScalarValue("tag1"), new ScalarValue("tag2")]),
            ["Nested"] = nestedStructure
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Complex log event with user {UserName} and ID {UserId}",
            properties: properties
        );

        // Act
        await sink.EmitBatchAsync([logEvent]);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(1);
        var doc = documents[0];
        var props = doc["Properties"].AsBsonDocument;

        props["UserId"].AsInt32.Should().Be(12345);
        props["UserName"].AsString.Should().Be("John Doe");
        props["IsActive"].AsBoolean.Should().BeTrue();
        props["Score"].AsDouble.Should().Be(98.5);
        props["Tags"].AsBsonArray.Count.Should().Be(2);
        props["Nested"].AsBsonDocument["InnerProp"].AsString.Should().Be("inner value");
    }

    [Fact]
    public async Task OnEmptyBatchAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = "logs"
        };

        var sink = new MongoSink(options);

        // Act & Assert
        await sink.OnEmptyBatchAsync();
        // Should complete without throwing
    }

    [Fact]
    public async Task EmitBatchAsync_WithDifferentTimestamps_ShouldPreserveTimestampOrder()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = collectionName
        };

        var sink = new MongoSink(options);
        var baseTime = DateTimeOffset.UtcNow;
        var logEvents = new[]
        {
            CreateLogEvent(baseTime.AddSeconds(-10), LogEventLevel.Information, "First"),
            CreateLogEvent(baseTime.AddSeconds(-5), LogEventLevel.Information, "Second"),
            CreateLogEvent(baseTime, LogEventLevel.Information, "Third")
        };

        // Act
        await sink.EmitBatchAsync(logEvents);

        // Assert
        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("Timestamp"))
            .ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCount(3);
        documents[0]["Message"].AsString.Should().Be("First");
        documents[1]["Message"].AsString.Should().Be("Second");
        documents[2]["Message"].AsString.Should().Be("Third");
    }

    [Fact]
    public async Task Constructor_WithoutDocumentFactory_ShouldUseDefaultFactory()
    {
        // Arrange & Act
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = $"logs_{Guid.NewGuid():N}"
        };

        var sink = new MongoSink(options);

        // Assert - verify it works by emitting a batch
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");
        await sink.EmitBatchAsync([logEvent]);

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Constructor_WithoutMongoFactory_ShouldUseDefaultFactory()
    {
        // Arrange & Act
        var options = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "MongoSinkTests",
            CollectionName = $"logs_{Guid.NewGuid():N}"
        };

        var sink = new MongoSink(options);

        // Assert - verify it works by emitting a batch
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");
        await sink.EmitBatchAsync([logEvent]);

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(options);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(1);
    }

    private static LogEvent CreateLogEvent(
        DateTimeOffset timestamp,
        LogEventLevel level,
        string messageTemplate,
        Exception? exception = null,
        Dictionary<string, LogEventPropertyValue>? properties = null,
        ActivityTraceId traceId = default,
        ActivitySpanId spanId = default)
    {
        var template = new MessageTemplateParser().Parse(messageTemplate);
        properties ??= new Dictionary<string, LogEventPropertyValue>();

        return new LogEvent(
            timestamp,
            level,
            exception,
            template,
            properties.Select(p => new LogEventProperty(p.Key, p.Value)),
            traceId,
            spanId
        );
    }

    // Custom test factories
    private class CustomDocumentFactory : IDocumentFactory
    {
        public int CreateDocumentCallCount { get; private set; }

        public BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options)
        {
            CreateDocumentCallCount++;
            var factory = new DocumentFactory();
            var document = factory.CreateDocument(logEvent, options);
            if (document == null)
                return null;

            document["CustomField"] = "CustomValue";
            return document;
        }
    }

    private class CustomMongoFactory : IMongoFactory
    {
        private readonly string _connectionString;
        private readonly MongoFactory _defaultFactory;

        public int GetCollectionCallCount { get; private set; }

        public CustomMongoFactory(string connectionString)
        {
            _connectionString = connectionString;
            _defaultFactory = new MongoFactory();
        }

        public ValueTask<IMongoClient> GetClient(MongoSinkOptions options)
        {
            var modifiedOptions = new MongoSinkOptions
            {
                ConnectionString = _connectionString,
                DatabaseName = options.DatabaseName,
                CollectionName = options.CollectionName
            };
            return _defaultFactory.GetClient(modifiedOptions);
        }

        public ValueTask<IMongoDatabase> GetDatabase(MongoSinkOptions options, IMongoClient? client = null)
        {
            var modifiedOptions = new MongoSinkOptions
            {
                ConnectionString = _connectionString,
                DatabaseName = options.DatabaseName,
                CollectionName = options.CollectionName
            };
            return _defaultFactory.GetDatabase(modifiedOptions, client);
        }

        public ValueTask<IMongoCollection<BsonDocument>> GetCollection(MongoSinkOptions options, IMongoDatabase? database = null)
        {
            GetCollectionCallCount++;
            var modifiedOptions = new MongoSinkOptions
            {
                ConnectionString = _connectionString,
                DatabaseName = options.DatabaseName,
                CollectionName = options.CollectionName
            };
            return _defaultFactory.GetCollection(modifiedOptions, database);
        }
    }

    private class FilteringDocumentFactory : IDocumentFactory
    {
        public BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options)
        {
            // Filter out Debug level logs
            if (logEvent.Level == LogEventLevel.Debug)
                return null;

            var factory = new DocumentFactory();
            return factory.CreateDocument(logEvent, options);
        }
    }
}
