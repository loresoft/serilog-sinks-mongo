# Serilog.Sinks.Mongo Sample Application

This sample application demonstrates how to use Serilog with MongoDB sink in various configurations.

## Prerequisites

- .NET 10 SDK
- MongoDB server running on `mongodb://localhost:27017`

## Architecture

The application uses a unified `LoggingService` (BackgroundService) that demonstrates comprehensive logging patterns. Each command configures Serilog differently, but all execute the same logging demonstrations to show consistent behavior across different MongoDB collection types.

## Usage

The application provides three sub-commands to demonstrate different MongoDB collection configurations:

### 1. Time Series Collection (`timed`)

Uses MongoDB Time Series collection with automatic data expiration.

```bash
dotnet run -- timed
```

**Features:**
- Time series optimized collection for time-stamped log data
- Automatic log expiration after 7 days
- Efficient storage for time-based queries
- Collection name: `logs-timeseries`

### 2. Capped Collection (`capped`)

Uses MongoDB Capped collection with size and document limits.

```bash
dotnet run -- capped
```

**Features:**
- Fixed-size collection (5 MB)
- Maximum 1000 documents
- Automatic removal of oldest logs when limits are reached
- FIFO (First In, First Out) behavior
- Collection name: `logs-capped`

### 3. Configuration-Based (`config`)

Uses Serilog configuration from `appsettings.json`.

```bash
dotnet run -- config
```

**Features:**
- Configuration loaded from `appsettings.json`
- Easy to modify without recompilation
- Standard collection with expiration settings from config
- Collection name: `logs-config`

## Logging Demonstrations

All commands execute the same comprehensive logging demonstrations:

### 1. **Log Level Examples**
- Trace, Debug, Information, Warning, Error, Critical
- Shows appropriate use of each level

### 2. **Structured Logging**
- Simple properties (UserId, IpAddress, timestamps)
- Complex object destructuring with `@` operator
- Sensor/metrics data (temperature, humidity, pressure)
- Performance metrics (response time, memory, connections, CPU)

### 3. **Operational Logging**
- CRUD operations (Create, Read, Update, Delete, Query)
- Operation status tracking (Success, Failed, Pending, Timeout)
- Duration and performance tracking
- Error codes and retry counts

### 4. **Exception Logging**
- Simple exceptions with context
- Nested exceptions with inner exceptions
- Business logic exceptions with custom data
- Error recovery patterns

### 5. **Scoped Logging**
- Request tracking with RequestId
- Correlation IDs across operations
- User context in log scopes
- Hierarchical logging context

## Configuration

Edit `appsettings.json` to customize:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "MongoDB",
        "Args": {
          "connectionString": "mongodb://localhost:27017",
          "databaseName": "serilog",
          "collectionName": "logs-config",
          "expireAfter": "30.00:00:00"
        }
      }
    ]
  }
}
```

## MongoDB Collections Created

After running the commands, the following collections will be created in the `serilog` database:

- `logs-timeseries` - Time series collection with 7-day expiration
- `logs-capped` - Capped collection (5 MB, 1000 documents max)
- `logs-config` - Standard collection with 30-day expiration (from config)

## Viewing Logs

Connect to MongoDB and query the logs:

```javascript
// Connect to MongoDB
use serilog

// View recent time series logs
db['logs-timeseries'].find().sort({ Timestamp: -1 }).limit(10)

// View capped collection logs
db['logs-capped'].find().sort({ $natural: -1 }).limit(10)

// View config-based logs
db['logs-config'].find().sort({ Timestamp: -1 }).limit(10)

// Query logs by level
db['logs-timeseries'].find({ Level: "Error" })

// Query logs with specific properties
db['logs-timeseries'].find({ "Properties.Operation": "Create" })

// Query with scoped properties
db['logs-timeseries'].find({ "Properties.RequestId": { $exists: true } })
```

## Key Concepts Demonstrated

1. **Host.CreateApplicationBuilder**: Modern .NET hosting model
2. **Serilog.Extensions.Hosting**: Integration with Microsoft.Extensions.Logging
3. **MongoDB Time Series**: Optimized for time-stamped data
4. **MongoDB Capped Collections**: Fixed-size collections with FIFO behavior
5. **Configuration-based setup**: Using appsettings.json for Serilog
6. **System.CommandLine**: Modern CLI argument parsing
7. **Structured logging**: Rich, queryable log data with properties
8. **Log scopes**: Correlation and context tracking across operations
9. **Exception handling**: Comprehensive error logging patterns
10. **Operational metrics**: Performance and business event tracking

## Code Organization

```
Services/
├── LoggingService.cs              # Unified logging demonstrations
└── SampleLoggingService.cs        # Additional logging pattern examples

Commands/
├── TimedCommand.cs                # Time series collection setup
├── CappedCommand.cs               # Capped collection setup
└── ConfigCommand.cs               # Configuration-based setup
```

## Benefits of This Approach

- **Consistency**: Same logging examples across all collection types
- **Simplicity**: No scenario switching logic needed
- **Clarity**: Easy to understand what each command does differently (collection config)
- **Maintainability**: Single source of logging examples
- **Comparability**: Easy to compare how different collection types handle the same logs
