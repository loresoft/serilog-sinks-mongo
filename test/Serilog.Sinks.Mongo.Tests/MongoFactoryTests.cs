using MongoDB.Bson;
using MongoDB.Driver;

using Serilog.Sinks.Mongo.Tests.Fixtures;

namespace Serilog.Sinks.Mongo.Tests;

public class MongoFactoryTests(DatabaseFixture databaseFixture) : DatabaseTestBase(databaseFixture)
{
    [Fact]
    public async Task GetClient_WithConnectionString_ShouldReturnClient()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString
        };
        var factory = new MongoFactory();

        // Act
        var client = await factory.GetClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClient_WithMongoUrl_ShouldReturnClient()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var mongoUrl = new MongoUrl(connectionString);
        var options = new MongoSinkOptions
        {
            MongoUrl = mongoUrl
        };
        var factory = new MongoFactory();

        // Act
        var client = await factory.GetClient(options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClient_WithoutConnectionStringOrUrl_ShouldUseDefaultLocalhost()
    {
        // Arrange
        var options = new MongoSinkOptions();
        var factory = new MongoFactory();

        // Act
        var client = await factory.GetClient(options);

        // Assert
        client.Should().NotBeNull();
        client.Settings.Server.Host.Should().Be("localhost");
        client.Settings.Server.Port.Should().Be(27017);
    }

    [Fact]
    public async Task GetClient_CalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString
        };
        var factory = new MongoFactory();

        // Act
        var client1 = await factory.GetClient(options);
        var client2 = await factory.GetClient(options);

        // Assert
        client1.Should().BeSameAs(client2);
    }

    [Fact]
    public async Task GetClient_WithMongoUrlPreferred_ShouldUseMongoUrlOverConnectionString()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var mongoUrl = new MongoUrl(connectionString);
        var options = new MongoSinkOptions
        {
            ConnectionString = "mongodb://invalid:27017",
            MongoUrl = mongoUrl
        };
        var factory = new MongoFactory();

        // Act
        var client = await factory.GetClient(options);

        // Assert
        client.Should().NotBeNull();
        // Should use MongoUrl, not the invalid connection string
    }

    [Fact]
    public async Task GetDatabase_WithDatabaseName_ShouldReturnDatabase()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests"
        };
        var factory = new MongoFactory();

        // Act
        var database = await factory.GetDatabase(options);

        // Assert
        database.Should().NotBeNull();
        database.DatabaseNamespace.DatabaseName.Should().Be("MongFactoryTests");
    }

    [Fact]
    public async Task GetDatabase_WithoutDatabaseName_ShouldUseDefaultSerilog()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = null
        };
        var factory = new MongoFactory();

        // Act
        var database = await factory.GetDatabase(options);

        // Assert
        database.Should().NotBeNull();
        database.DatabaseNamespace.DatabaseName.Should().Be("serilog");
    }

    [Fact]
    public async Task GetDatabase_CalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests"
        };
        var factory = new MongoFactory();

        // Act
        var database1 = await factory.GetDatabase(options);
        var database2 = await factory.GetDatabase(options);

        // Assert
        database1.Should().BeSameAs(database2);
    }

    [Fact]
    public async Task GetDatabase_WithExplicitClient_ShouldUseProvidedClient()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests"
        };
        var factory = new MongoFactory();
        var client = await factory.GetClient(options);

        // Act
        var database = await factory.GetDatabase(options, client);

        // Assert
        database.Should().NotBeNull();
        database.Client.Should().BeSameAs(client);
    }

    [Fact]
    public async Task GetDatabase_WithoutExplicitClient_ShouldCreateClientAutomatically()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests"
        };
        var factory = new MongoFactory();

        // Act
        var database = await factory.GetDatabase(options);

        // Assert
        database.Should().NotBeNull();
        database.Client.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCollection_WithCollectionName_ShouldReturnCollection()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = "TestLogs"
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();
        collection.CollectionNamespace.CollectionName.Should().Be("TestLogs");
    }

    [Fact]
    public async Task GetCollection_WithoutCollectionName_ShouldUseDefaultLogs()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = null
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();
        collection.CollectionNamespace.CollectionName.Should().Be("logs");
    }

    [Fact]
    public async Task GetCollection_CalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = "CachedLogs"
        };
        var factory = new MongoFactory();

        // Act
        var collection1 = await factory.GetCollection(options);
        var collection2 = await factory.GetCollection(options);

        // Assert
        collection1.Should().BeSameAs(collection2);
    }

    [Fact]
    public async Task GetCollection_WithExplicitDatabase_ShouldUseProvidedDatabase()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = "TestLogs"
        };
        var factory = new MongoFactory();
        var database = await factory.GetDatabase(options);

        // Act
        var collection = await factory.GetCollection(options, database);

        // Assert
        collection.Should().NotBeNull();
        collection.Database.Should().BeSameAs(database);
    }

    [Fact]
    public async Task GetCollection_WithoutExplicitDatabase_ShouldCreateDatabaseAutomatically()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = "TestLogs"
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();
        collection.Database.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCollection_WhenCollectionDoesNotExist_ShouldNotCreateCollectionWithoutOptions()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var uniqueCollectionName = $"NewCollection_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = uniqueCollectionName
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();

        // Verify collection was NOT created (no CollectionOptions specified, so auto-creation on write)
        var database = collection.Database;
        var collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        var collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().NotContain(uniqueCollectionName);
        
        // Insert a document to trigger auto-creation
        var testDocument = new BsonDocument { { "test", "value" }, { "Timestamp", DateTime.UtcNow } };
        await collection.InsertOneAsync(testDocument, cancellationToken: TestCancellation);
        
        // Now verify collection exists after write
        collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().Contain(uniqueCollectionName);
    }

    [Fact]
    public async Task GetCollection_WithCustomCollectionOptions_ShouldCreateCollectionWithOptions()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var uniqueCollectionName = $"CustomCollection_{Guid.NewGuid():N}";
        var collectionOptions = new CreateCollectionOptions
        {
            TimeSeriesOptions = new TimeSeriesOptions("Timestamp"),
            ExpireAfter = TimeSpan.FromDays(7)
        };
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = uniqueCollectionName,
            CollectionOptions = collectionOptions
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();

        // Verify collection was created
        var database = collection.Database;
        var collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        var collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().Contain(uniqueCollectionName);
    }

    [Fact]
    public async Task GetCollection_WithCappedCollectionOptions_ShouldCreateCappedCollection()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var uniqueCollectionName = $"CappedCollection_{Guid.NewGuid():N}";
        var collectionOptions = new CreateCollectionOptions
        {
            Capped = true,
            MaxSize = 1024 * 1024, // 1MB
            MaxDocuments = 1000
        };
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = uniqueCollectionName,
            CollectionOptions = collectionOptions
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();

        // Verify collection was created
        var database = collection.Database;
        var collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        var collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().Contain(uniqueCollectionName);

        // Verify it's a capped collection by checking collection stats
        var command = new BsonDocument { { "collStats", uniqueCollectionName } };
        var stats = await database.RunCommandAsync<BsonDocument>(command, cancellationToken: TestCancellation);
        stats["capped"].AsBoolean.Should().BeTrue();
        stats["maxSize"].ToInt64().Should().Be(1024 * 1024);
    }

    [Fact]
    public async Task GetCollection_WithoutCollectionOptions_ShouldAllowAutoCreation()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var uniqueCollectionName = $"AutoCreatedCollection_{Guid.NewGuid():N}";
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = uniqueCollectionName,
            CollectionOptions = null
        };
        var factory = new MongoFactory();

        // Act
        var collection = await factory.GetCollection(options);

        // Assert
        collection.Should().NotBeNull();

        // Verify collection was NOT created yet (auto-creation happens on first write)
        var database = collection.Database;
        var collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        var collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().NotContain(uniqueCollectionName);
        
        // Write to trigger auto-creation
        var testDocument = new BsonDocument { { "test", "value" }, { "Timestamp", DateTime.UtcNow } };
        await collection.InsertOneAsync(testDocument, cancellationToken: TestCancellation);
        
        // Now it should exist
        collectionList = await database.ListCollectionNamesAsync(cancellationToken: TestCancellation);
        collections = await collectionList.ToListAsync(cancellationToken: TestCancellation);
        collections.Should().Contain(uniqueCollectionName);
    }

    [Fact]
    public async Task GetClient_ConcurrentCalls_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString
        };
        var factory = new MongoFactory();

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => factory.GetClient(options).AsTask())
            .ToArray();

        var clients = await Task.WhenAll(tasks);

        // Assert
        clients.Should().AllBeEquivalentTo(clients[0]);
        clients.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDatabase_ConcurrentCalls_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests"
        };
        var factory = new MongoFactory();

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => factory.GetDatabase(options).AsTask())
            .ToArray();

        var databases = await Task.WhenAll(tasks);

        // Assert
        databases.Should().AllBeEquivalentTo(databases[0]);
        databases.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCollection_ConcurrentCalls_ShouldReturnSameInstance()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = $"ConcurrentCollection_{Guid.NewGuid():N}"
        };
        var factory = new MongoFactory();

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => factory.GetCollection(options).AsTask())
            .ToArray();

        var collections = await Task.WhenAll(tasks);

        // Assert
        collections.Should().AllBeEquivalentTo(collections[0]);
        collections.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task Integration_GetClientThenDatabaseThenCollection_ShouldWorkTogether()
    {
        // Arrange
        var connectionString = Fixture.GetConnectionString();
        var options = new MongoSinkOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "MongFactoryTests",
            CollectionName = "IntegrationLogs"
        };
        var factory = new MongoFactory();

        // Act
        var client = await factory.GetClient(options);
        var database = await factory.GetDatabase(options, client);
        var collection = await factory.GetCollection(options, database);

        // Assert
        client.Should().NotBeNull();
        database.Should().NotBeNull();
        collection.Should().NotBeNull();

        database.Client.Should().BeSameAs(client);
        collection.Database.Should().BeSameAs(database);
        collection.Database.DatabaseNamespace.DatabaseName.Should().Be("MongFactoryTests");
        collection.CollectionNamespace.CollectionName.Should().Be("IntegrationLogs");
    }
}
