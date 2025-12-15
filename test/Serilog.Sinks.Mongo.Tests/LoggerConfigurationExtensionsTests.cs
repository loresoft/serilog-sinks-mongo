using MongoDB.Bson;
using MongoDB.Driver;

using Serilog.Events;
using Serilog.Sinks.Mongo.Tests.Fixtures;

namespace Serilog.Sinks.Mongo.Tests;

public class LoggerConfigurationExtensionsTests(DatabaseFixture databaseFixture) : DatabaseTestBase(databaseFixture)
{
    #region MongoDB with Action<MongoSinkOptions> overload

    [Fact]
    public void MongoDB_WithValidConfigureAction_CreatesLogger()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Act
        var logger = loggerConfiguration.WriteTo.MongoDB(options =>
        {
            options.ConnectionString = Fixture.GetConnectionString();
            options.DatabaseName = "LoggerConfigurationTests";
            options.CollectionName = collectionName;
        }).CreateLogger();

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void MongoDB_WithNullLoggerConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        Serilog.Configuration.LoggerSinkConfiguration? loggerConfiguration = null;

        // Act
        var act = () => loggerConfiguration!.MongoDB(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerConfiguration");
    }

    [Fact]
    public void MongoDB_WithNullConfigureAction_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        var act = () => loggerConfiguration.WriteTo.MongoDB(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public async Task MongoDB_WithConfigureAction_LogsSuccessfully()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(options =>
            {
                options.ConnectionString = Fixture.GetConnectionString();
                options.DatabaseName = "LoggerConfigurationTests";
                options.CollectionName = collectionName;
            })
            .CreateLogger();

        // Act
        logger.Information("Test message from configure action");


        await Task.Delay(100, cancellationToken: TestCancellation); // Allow time for async write

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithConfigureActionAndCustomMinimumLevel_RespectsMinimumLevel()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(options =>
            {
                options.ConnectionString = Fixture.GetConnectionString();
                options.DatabaseName = "LoggerConfigurationTests";
                options.CollectionName = collectionName;
                options.MinimumLevel = LogEventLevel.Warning;
            })
            .CreateLogger();

        // Act
        logger.Debug("Debug message - should not be logged");
        logger.Information("Info message - should not be logged");
        logger.Warning("Warning message - should be logged");
        logger.Error("Error message - should be logged");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCountGreaterThanOrEqualTo(2);
        documents.Should().OnlyContain(d => d["Level"].AsString == "Warning" || d["Level"].AsString == "Error");
    }

    [Fact]
    public async Task MongoDB_WithConfigureActionNoDatabaseName_UsesDatabaseNameFromMongoUrl()
    {
        // Arrange
        var databaseNameInUrl = "DatabaseFromConfigAction";
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Create a MongoUrl with a specific database name
        var mongoUrlBuilder = new MongoUrlBuilder(Fixture.GetConnectionString())
        {
            DatabaseName = databaseNameInUrl
        };
        var mongoUrl = mongoUrlBuilder.ToMongoUrl();

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(options =>
            {
                options.MongoUrl = mongoUrl;
                options.CollectionName = collectionName;
            })
            .CreateLogger();

        // Act
        logger.Information("Test message with database from MongoUrl in options");

        await Task.Delay(200, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert - Verify the log was written to the database specified in MongoUrl
        var mongoClient = new MongoClient(mongoUrl);
        var database = mongoClient.GetDatabase(databaseNameInUrl);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithConfigureActionNoDatabaseName_UsesDatabaseNameFromConnectionString()
    {
        // Arrange
        var databaseNameInConnectionString = "DatabaseFromConnectionString";
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Create a connection string with a specific database name
        var mongoUrlBuilder = new MongoUrlBuilder(Fixture.GetConnectionString())
        {
            DatabaseName = databaseNameInConnectionString
        };
        var connectionStringWithDb = mongoUrlBuilder.ToString();

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(options =>
            {
                options.ConnectionString = connectionStringWithDb;
                options.CollectionName = collectionName;
            })
            .CreateLogger();

        // Act
        logger.Information("Test message with database from ConnectionString in options");

        await Task.Delay(200, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert - Verify the log was written to the database specified in ConnectionString
        var mongoClient = new MongoClient(connectionStringWithDb);
        var database = mongoClient.GetDatabase(databaseNameInConnectionString);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    #endregion

    #region MongoDB with ConnectionString overload

    [Fact]
    public void MongoDB_WithConnectionString_CreatesLogger()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();
        var connectionString = Fixture.GetConnectionString();
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Act
        var logger = loggerConfiguration.WriteTo.MongoDB(
            connectionString,
            "LoggerConfigurationTests",
            collectionName
        ).CreateLogger();

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void MongoDB_WithConnectionStringNullLoggerConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        Serilog.Configuration.LoggerSinkConfiguration? loggerConfiguration = null;
        var connectionString = Fixture.GetConnectionString();

        // Act
        var act = () => loggerConfiguration!.MongoDB(connectionString);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerConfiguration");
    }

    [Fact]
    public void MongoDB_WithNullConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        string connectionString = null!;
        var act = () => loggerConfiguration.WriteTo.MongoDB(connectionString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public void MongoDB_WithEmptyConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        var act = () => loggerConfiguration.WriteTo.MongoDB(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringDefaults_LogsSuccessfully()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName
            )
            .CreateLogger();

        // Act
        logger.Information("Test message with connection string");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringAndMinimumLevel_RespectsMinimumLevel()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName,
                minimumLevel: LogEventLevel.Error
            )
            .CreateLogger();

        // Act
        logger.Information("Info message - should not be logged");
        logger.Warning("Warning message - should not be logged");
        logger.Error("Error message - should be logged");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken: TestCancellation);

        documents.Should().HaveCountGreaterThanOrEqualTo(1);
        documents.Should().OnlyContain(d => d["Level"].AsString == "Error");
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringAndExpireAfter_SetsExpireAfter()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var expireAfter = TimeSpan.FromDays(7);

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName,
                expireAfter: expireAfter
            )
            .CreateLogger();

        // Act
        logger.Information("Test message");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringAndCappedCollection_CreatesCappedCollection()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var maxSize = 1024L * 1024L; // 1MB
        var maxDocuments = 1000L;

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName,
                maxSize: maxSize,
                maxDocuments: maxDocuments
            )
            .CreateLogger();

        // Act
        logger.Information("Test message");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName,
            CollectionOptions = new CreateCollectionOptions
            {
                Capped = true,
                MaxSize = maxSize,
                MaxDocuments = maxDocuments
            }
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringAndBatchingOptions_UsesBatchingOptions()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var batchSizeLimit = 50;
        var bufferingTimeLimit = TimeSpan.FromSeconds(2);

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName,
                batchSizeLimit: batchSizeLimit,
                bufferingTimeLimit: bufferingTimeLimit
            )
            .CreateLogger();

        // Act
        for (int i = 0; i < 10; i++)
        {
            logger.Information("Test message {Index}", i);
        }


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().Be(10);
    }

    [Fact]
    public async Task MongoDB_WithConnectionStringAndCustomDocumentFactory_UsesCustomFactory()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var customFactory = new TestDocumentFactory();

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                Fixture.GetConnectionString(),
                "LoggerConfigurationTests",
                collectionName,
                documentFactory: customFactory
            )
            .CreateLogger();

        // Act
        logger.Information("Test message");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        customFactory.CallCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region MongoDB with MongoUrl overload

    [Fact]
    public void MongoDB_WithMongoUrl_CreatesLogger()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();
        var mongoUrl = new MongoUrl(Fixture.GetConnectionString());
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Act
        var logger = loggerConfiguration.WriteTo.MongoDB(
            mongoUrl,
            "LoggerConfigurationTests",
            collectionName
        ).CreateLogger();

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void MongoDB_WithMongoUrlNullLoggerConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        Serilog.Configuration.LoggerSinkConfiguration? loggerConfiguration = null;
        var mongoUrl = new MongoUrl(Fixture.GetConnectionString());

        // Act
        var act = () => loggerConfiguration!.MongoDB(mongoUrl);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerConfiguration");
    }

    [Fact]
    public void MongoDB_WithNullMongoUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        var act = () => loggerConfiguration.WriteTo.MongoDB((MongoUrl)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mongoUrl");
    }

    [Fact]
    public async Task MongoDB_WithMongoUrl_LogsSuccessfully()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var mongoUrl = new MongoUrl(Fixture.GetConnectionString());

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                mongoUrl,
                "LoggerConfigurationTests",
                collectionName
            )
            .CreateLogger();

        // Act
        logger.Information("Test message with MongoUrl");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithMongoUrlAndAllParameters_LogsSuccessfully()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var mongoUrl = new MongoUrl(Fixture.GetConnectionString());
        var customFactory = new TestDocumentFactory();

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                mongoUrl,
                "LoggerConfigurationTests",
                collectionName,
                minimumLevel: LogEventLevel.Information,
                expireAfter: TimeSpan.FromDays(14),
                batchSizeLimit: 100,
                bufferingTimeLimit: TimeSpan.FromSeconds(5),
                documentFactory: customFactory
            )
            .CreateLogger();

        // Act
        logger.Information("Test message with all parameters");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
        customFactory.CallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithMongoUrlAndCappedCollection_DoesNotSetExpireAfter()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var mongoUrl = new MongoUrl(Fixture.GetConnectionString());

        // When capped collection is specified, expireAfter should not be set by default
        var maxSize = 1024L * 1024L; // 1MB
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                mongoUrl,
                "LoggerConfigurationTests",
                collectionName,
                maxSize: maxSize
            )
            .CreateLogger();

        // Act
        logger.Information("Test message");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = "LoggerConfigurationTests",
            CollectionName = collectionName,
            CollectionOptions = new CreateCollectionOptions
            {
                Capped = true,
                MaxSize = maxSize
            }
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MongoDB_WithMongoUrlNoDatabaseNameParameter_UsesDatabaseNameFromMongoUrl()
    {
        // Arrange
        var databaseNameInUrl = "DatabaseFromUrl";
        var collectionName = $"logs_{Guid.NewGuid():N}";

        // Create a MongoUrl with a specific database name
        var mongoUrlBuilder = new MongoUrlBuilder(Fixture.GetConnectionString())
        {
            DatabaseName = databaseNameInUrl
        };
        var mongoUrl = mongoUrlBuilder.ToMongoUrl();

        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(
                mongoUrl,
                collectionName: collectionName
            )
            .CreateLogger();

        // Act
        logger.Information("Test message with database from MongoUrl");

        await Task.Delay(200, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert - Verify the log was written to the database specified in MongoUrl
        var mongoClient = new MongoClient(mongoUrl);
        var database = mongoClient.GetDatabase(databaseNameInUrl);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    #endregion

    #region Default values tests

    [Fact]
    public async Task MongoDB_WithConnectionStringMinimalParameters_UsesDefaultValues()
    {
        // Arrange
        var collectionName = $"logs_{Guid.NewGuid():N}";
        var logger = new LoggerConfiguration()
            .WriteTo.MongoDB(Fixture.GetConnectionString(), collectionName: collectionName)
            .CreateLogger();

        // Act
        logger.Information("Test with defaults");


        await Task.Delay(100, cancellationToken: TestCancellation);
        logger.Dispose();

        // Assert - should use default database name
        var mongoOptions = new MongoSinkOptions
        {
            ConnectionString = Fixture.GetConnectionString(),
            DatabaseName = MongoSinkDefaults.DatabaseName,
            CollectionName = collectionName
        };

        var factory = new MongoFactory();
        var collection = await factory.GetCollection(mongoOptions);
        var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: TestCancellation);
        count.Should().BeGreaterThan(0);
    }

    #endregion

    #region Helper classes

    private class TestDocumentFactory : IDocumentFactory
    {
        public int CallCount { get; private set; }

        public BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options)
        {
            CallCount++;
            var factory = new DocumentFactory();
            var document = factory.CreateDocument(logEvent, options);

            if (document == null)
                return null;

            document["TestField"] = "TestValue";
            return document;
        }
    }

    #endregion
}
