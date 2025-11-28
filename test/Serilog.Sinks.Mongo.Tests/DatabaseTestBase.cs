using Serilog.Sinks.Mongo.Tests.Fixtures;

namespace Serilog.Sinks.Mongo.Tests;

[Collection(DatabaseCollection.CollectionName)]
public abstract class DatabaseTestBase(DatabaseFixture databaseFixture)
    : TestHostBase<DatabaseFixture>(databaseFixture)
{
}
