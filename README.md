# Serilog.Sinks.Mongo

[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.Mongo.svg)](https://www.nuget.org/packages/Serilog.Sinks.Mongo/)
[![License](https://img.shields.io/github/license/loresoft/serilog-sinks-mongo.svg)](https://github.com/loresoft/serilog-sinks-mongo/blob/main/LICENSE)

A high-performance [Serilog](https://serilog.net/) sink that writes log events to [MongoDB](https://www.mongodb.com/). This sink provides efficient batching, flexible configuration, and support for MongoDB-specific features like TTL indexes and capped collections.

## Features

- **Batched Writes** - Efficient batch processing of log events
- **Automatic Expiration** - TTL index support for automatic log rotation
- **Capped Collections** - Size and document count limited collections
- **Flexible Configuration** - Code-based and configuration file support
- **Customizable Document Format** - Extensible document factory pattern
- **High Performance** - Asynchronous writes with configurable buffering

## Installation

Install via NuGet:

```bash
dotnet add package Serilog.Sinks.Mongo
```

Or using Package Manager Console:

```powershell
Install-Package Serilog.Sinks.Mongo
```

## Quick Start

### Basic Usage

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "logs"
    )
    .CreateLogger();

Log.Information("Hello, MongoDB!");
Log.CloseAndFlush();
```

### With TTL Index (Automatic Expiration)

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "logs",
        expireAfter: TimeSpan.FromDays(30) // Logs expire after 30 days
    )
    .CreateLogger();
```

### With Capped Collection

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "logs",
        maxDocuments: 10000, // Maximum 10,000 documents
        maxSize: 10485760    // Maximum 10 MB
    )
    .CreateLogger();
```

## Configuration

### Code-Based Configuration

#### Using Connection String

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "logs",
        minimumLevel: LogEventLevel.Information,
        expireAfter: TimeSpan.FromDays(7),
        batchSizeLimit: 100,
        bufferingTimeLimit: TimeSpan.FromSeconds(2)
    )
    .CreateLogger();
```

#### Using MongoUrl

```csharp
var mongoUrl = new MongoUrl("mongodb://localhost:27017");

Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        mongoUrl: mongoUrl,
        databaseName: "serilog",
        collectionName: "logs"
    )
    .CreateLogger();
```

#### Using Options Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "serilog";
        options.CollectionName = "logs";
        options.MinimumLevel = LogEventLevel.Debug;
        options.ExpireAfter = TimeSpan.FromDays(30);
        options.BatchSizeLimit = 100;
        options.BufferingTimeLimit = TimeSpan.FromSeconds(5);
        
        // Capped collection options
        options.CollectionOptions = new CreateCollectionOptions
        {
            Capped = true,
            MaxSize = 5242880,    // 5 MB
            MaxDocuments = 1000
        };
        
        // Custom properties to promote to top-level
        options.Properties = new HashSet<string> 
        { 
            "SourceContext", 
            "RequestId",
            "UserId"
        };
    })
    .CreateLogger();
```

### JSON Configuration (appsettings.json)

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Mongo", "Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "MongoDB",
        "Args": {
          "connectionString": "mongodb://localhost:27017",
          "databaseName": "serilog",
          "collectionName": "logs",
          "expireAfter": "30.00:00:00"
        }
      }
    ],
    "Enrich": ["FromLogContext"]
  }
}
```

Then in your code:

```csharp
using Serilog;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

## Configuration Options

### MongoSinkOptions

| Property             | Type                      | Default             | Description                                 |
| -------------------- | ------------------------- | ------------------- | ------------------------------------------- |
| `ConnectionString`   | `string`                  | -                   | MongoDB connection string                   |
| `MongoUrl`           | `MongoUrl`                | -                   | Alternative to ConnectionString             |
| `DatabaseName`       | `string`                  | `"serilog"`         | Database name                               |
| `CollectionName`     | `string`                  | `"logs"`            | Collection name                             |
| `MinimumLevel`       | `LogEventLevel`           | `Verbose`           | Minimum log level to write                  |
| `ExpireAfter`        | `TimeSpan?`               | -                   | TTL for automatic document expiration       |
| `BatchSizeLimit`     | `int`                     | `100`               | Maximum batch size                          |
| `BufferingTimeLimit` | `TimeSpan`                | `00:00:02`          | Maximum time to wait before writing a batch |
| `CollectionOptions`  | `CreateCollectionOptions` | -                   | MongoDB collection creation options         |
| `Properties`         | `HashSet<string>`         | `{"SourceContext"}` | Properties to promote to top-level          |
| `DocumentFactory`    | `IDocumentFactory`        | -                   | Custom document factory                     |
| `MongoFactory`       | `IMongoFactory`           | -                   | Custom MongoDB factory                      |

## Document Structure

By default, log events are stored with the following structure:

```json
{
  "_id": ObjectId("..."),
  "Timestamp": ISODate("2025-11-27T10:30:00.000Z"),
  "Level": "Information",
  "Message": "User logged in successfully",
  "TraceId": "00-abc123...",
  "SpanId": "def456...",
  "SourceContext": "MyApp.Controllers.AuthController",
  "Properties": {
    "UserId": "12345",
    "Username": "john.doe",
    "IPAddress": "192.168.1.1"
  },
  "Exception": {
    "Message": "...",
    "Type": "System.Exception",
    "Text": "...",
    "HResult": -2146233088
  }
}
```

### Property Promotion

Properties can be promoted from the `Properties` object to the top level of the document:

```csharp
options.Properties = new HashSet<string> 
{ 
    "SourceContext",
    "RequestId",
    "UserId",
    "MachineName"
};
```

This results in:

```json
{
  "_id": ObjectId("..."),
  "Timestamp": ISODate("2025-11-27T10:30:00.000Z"),
  "Level": "Information",
  "Message": "Processing request",
  "SourceContext": "MyApp.Services.ProcessingService",
  "RequestId": "req-789",
  "UserId": "12345",
  "MachineName": "WEB-SERVER-01",
  "Properties": {
    // Other properties...
  }
}
```

## Custom Document Factory

Implement `IDocumentFactory` to customize the document structure:

```csharp
public class CustomDocumentFactory : DocumentFactory
{
    public override BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options)
    {
        var document = base.CreateDocument(logEvent, options);
        
        if (document != null)
        {
            // Add custom fields
            document["Application"] = "MyApp";
            document["Environment"] = "Production";
            
            // Custom transformations
            if (logEvent.Properties.TryGetValue("RequestPath", out var path))
            {
                document["Path"] = path.ToString();
            }
        }
        
        return document;
    }
}

// Use the custom factory
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "serilog";
        options.CollectionName = "logs";
        options.DocumentFactory = new CustomDocumentFactory();
    })
    .CreateLogger();
```

## Advanced Scenarios

### Multiple Sinks with Different Configurations

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "errors",
        minimumLevel: LogEventLevel.Error
    )
    .WriteTo.MongoDB(
        connectionString: "mongodb://localhost:27017",
        databaseName: "serilog",
        collectionName: "all-logs",
        minimumLevel: LogEventLevel.Information,
        expireAfter: TimeSpan.FromDays(7)
    )
    .CreateLogger();
```

### With Microsoft.Extensions.Hosting

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(loggerConfiguration =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.MongoDB(
            connectionString: "mongodb://localhost:27017",
            databaseName: "serilog",
            collectionName: "logs",
            expireAfter: TimeSpan.FromDays(30)
        );
});

var host = builder.Build();
await host.RunAsync();
```

## Performance Tuning

### Batch Configuration

Adjust batching settings based on your throughput requirements:

```csharp
options.BatchSizeLimit = 500;           // Larger batches for high throughput
options.BufferingTimeLimit = TimeSpan.FromSeconds(10); // Longer wait for batch fill
```

## MongoDB Collection Strategies

### TTL Index (Time-Based Expiration)

Best for applications that need automatic log cleanup:

```csharp
.WriteTo.MongoDB(
    connectionString: "mongodb://localhost:27017",
    databaseName: "serilog",
    collectionName: "logs",
    expireAfter: TimeSpan.FromDays(30)
)
```

A TTL index is automatically created on the `Timestamp` field to removes expired documents.

### Capped Collection (Size/Count Limited)

Best for fixed-size log storage:

```csharp
.WriteTo.MongoDB(
    connectionString: "mongodb://localhost:27017",
    databaseName: "serilog",
    collectionName: "logs",
    maxDocuments: 100000,  // Keep latest 100k documents
    maxSize: 104857600     // Or 100 MB, whichever is hit first
)
```

Oldest documents are automatically removed when limits are reached.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
