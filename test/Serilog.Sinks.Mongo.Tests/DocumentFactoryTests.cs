using System.Diagnostics;
using System.Runtime.InteropServices;

using MongoDB.Bson;

using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Sinks.Mongo.Tests;

public class DocumentFactoryTests
{
    private readonly DocumentFactory _factory;
    private readonly MongoSinkOptions _options;

    public DocumentFactoryTests()
    {
        _factory = new DocumentFactory();
        _options = new MongoSinkOptions();
    }

    [Fact]
    public void CreateDocument_WithBasicLogEvent_ShouldCreateDocumentWithRequiredFields()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var logEvent = CreateLogEvent(
            timestamp,
            LogEventLevel.Information,
            "Test message"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document["Timestamp"].Should().BeOfType<BsonDateTime>();

        // BsonDateTime truncates milliseconds to 3 decimal places
        var actualTime = document["Timestamp"].AsBsonDateTime.ToUniversalTime();

        var expectedTime = timestamp.UtcDateTime;
        (actualTime - expectedTime).TotalMilliseconds.Should().BeLessThan(1);

        document["Level"].Should().BeOfType<BsonString>();
        document["Level"].AsString.Should().Be("Information");
        document["Message"].Should().BeOfType<BsonString>();
        document["Message"].AsString.Should().Be("Test message");
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "Verbose")]
    [InlineData(LogEventLevel.Debug, "Debug")]
    [InlineData(LogEventLevel.Information, "Information")]
    [InlineData(LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, "Error")]
    [InlineData(LogEventLevel.Fatal, "Fatal")]
    public void CreateDocument_WithDifferentLogLevels_ShouldConvertCorrectly(LogEventLevel level, string expectedLevelName)
    {
        // Arrange
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, level, "Test");

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document["Level"].AsString.Should().Be(expectedLevelName);
    }

    [Fact]
    public void CreateDocument_WithTraceId_ShouldIncludeTraceId()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom();
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            traceId: traceId
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("TraceId").Should().BeTrue();
        document["TraceId"].AsString.Should().Be(traceId.ToHexString());
    }

    [Fact]
    public void CreateDocument_WithSpanId_ShouldIncludeSpanId()
    {
        // Arrange
        var spanId = ActivitySpanId.CreateRandom();
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            spanId: spanId
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("SpanId").Should().BeTrue();
        document["SpanId"].AsString.Should().Be(spanId.ToHexString());
    }

    [Fact]
    public void CreateDocument_WithoutTraceId_ShouldNotIncludeTraceId()
    {
        // Arrange
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("TraceId").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithoutSpanId_ShouldNotIncludeSpanId()
    {
        // Arrange
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("SpanId").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithException_ShouldIncludeExceptionDetails()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new InvalidOperationException("Test exception");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "Error occurred",
            exception: exception
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("Exception").Should().BeTrue();

        var exDoc = document["Exception"].AsBsonDocument;
        exDoc.Should().NotBeNull();

        exDoc["Message"].AsString.Should().Be("Test exception");
        exDoc["Type"].AsString.Should().Be(typeof(InvalidOperationException).ToString());

        exDoc.Contains("Text").Should().BeTrue();
        exDoc.Contains("BaseMessage").Should().BeTrue();
        exDoc.Contains("HResult").Should().BeTrue();
        exDoc.Contains("Source").Should().BeTrue();
    }

    [Fact]
    public void CreateDocument_WithExternalException_ShouldIncludeErrorCode()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new ExternalException("External error", 123);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "Error occurred",
            exception: exception
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();

        var exDoc = document["Exception"].AsBsonDocument;

        exDoc.Contains("ErrorCode").Should().BeTrue();
        exDoc["ErrorCode"].AsInt32.Should().Be(123);
    }

    [Fact]
    public void CreateDocument_WithExceptionWithTargetSite_ShouldIncludeMethodDetails()
    {
        // Arrange
        Exception? exception = null;
        try
        {
            ThrowException();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "Error occurred",
            exception: exception!
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();

        var exDoc = document["Exception"].AsBsonDocument;

        exDoc.Contains("MethodName").Should().BeTrue();
        exDoc["MethodName"].AsString.Should().Be(nameof(ThrowException));
        exDoc.Contains("ModuleName").Should().BeTrue();
    }

    private static void ThrowException()
    {
        throw new InvalidOperationException("Test exception with target site");
    }

    [Fact]
    public void CreateDocument_WithoutException_ShouldNotIncludeException()
    {
        // Arrange
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("Exception").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithScalarProperties_ShouldIncludeProperties()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["StringProp"] = new ScalarValue("test"),
            ["IntProp"] = new ScalarValue(42),
            ["BoolProp"] = new ScalarValue(true),
            ["DoubleProp"] = new ScalarValue(3.14)
        };
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("Properties").Should().BeTrue();

        var props = document["Properties"].AsBsonDocument;

        props["StringProp"].AsString.Should().Be("test");
        props["IntProp"].AsInt32.Should().Be(42);
        props["BoolProp"].AsBoolean.Should().BeTrue();
        props["DoubleProp"].AsDouble.Should().Be(3.14);
    }

    [Fact]
    public void CreateDocument_WithSequenceProperty_ShouldCreateBsonArray()
    {
        // Arrange
        var sequence = new SequenceValue([
            new ScalarValue(1),
            new ScalarValue(2),
            new ScalarValue(3)
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Numbers"] = sequence
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;

        props["Numbers"].Should().BeOfType<BsonArray>();

        var array = props["Numbers"].AsBsonArray;

        array.Count.Should().Be(3);
        array[0].AsInt32.Should().Be(1);
        array[1].AsInt32.Should().Be(2);
        array[2].AsInt32.Should().Be(3);
    }

    [Fact]
    public void CreateDocument_WithStructureProperty_ShouldCreateNestedDocument()
    {
        // Arrange
        var structure = new StructureValue([
            new LogEventProperty("Name", new ScalarValue("John")),
            new LogEventProperty("Age", new ScalarValue(30))
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["User"] = structure
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;

        props["User"].Should().BeOfType<BsonDocument>();

        var user = props["User"].AsBsonDocument;

        user["Name"].AsString.Should().Be("John");
        user["Age"].AsInt32.Should().Be(30);
    }

    [Fact]
    public void CreateDocument_WithDictionaryProperty_ShouldCreateDocument()
    {
        // Arrange
        var dictionary = new DictionaryValue([
            new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                new ScalarValue("key1"),
                new ScalarValue("value1")
            ),
            new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                new ScalarValue("key2"),
                new ScalarValue(42)
            )
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Dict"] = dictionary
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Dict"].Should().BeOfType<BsonDocument>();

        var dict = props["Dict"].AsBsonDocument;

        // Dictionary keys are converted using ToString(), which for ScalarValue includes quotes
        dict.Contains("\"key1\"").Should().BeTrue();
        dict.Contains("\"key2\"").Should().BeTrue();
        dict["\"key1\""].AsString.Should().Be("value1");
        dict["\"key2\""].AsInt32.Should().Be(42);
    }

    [Fact]
    public void CreateDocument_WithNoProperties_ShouldNotIncludePropertiesField()
    {
        // Arrange
        var logEvent = CreateLogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, "Test");

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document.Contains("Properties").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithPropertyNameContainingDot_ShouldSanitize()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Property.With.Dots"] = new ScalarValue("value")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;

        props.Contains("Property.With.Dots").Should().BeFalse();
        props.Contains("Property_With_Dots").Should().BeTrue();
        props["Property_With_Dots"].AsString.Should().Be("value");
    }

    [Fact]
    public void CreateDocument_WithPropertyNameContainingDollarSign_ShouldSanitize()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["$property"] = new ScalarValue("value")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;

        props.Contains("$property").Should().BeFalse();
        props.Contains("_property").Should().BeTrue();
    }

    [Fact]
    public void CreateDocument_WithPropertyNameContainingControlCharacters_ShouldSanitize()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["prop\u0000erty"] = new ScalarValue("value")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props.Contains("prop_erty").Should().BeTrue();
    }

    [Fact]
    public void CreateDocument_WithNullValue_ShouldHandleGracefully()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["NullProp"] = new ScalarValue(null)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;

        props.Contains("NullProp").Should().BeTrue();
        props["NullProp"].Should().BeOfType<BsonNull>();
    }

    [Fact]
    public void CreateDocument_WithComplexNestedStructure_ShouldHandleCorrectly()
    {
        // Arrange
        var nestedStructure = new StructureValue([
            new LogEventProperty("InnerProp", new ScalarValue("inner"))
        ]);

        var structure = new StructureValue([
            new LogEventProperty("Name", new ScalarValue("Test")),
            new LogEventProperty("Nested", nestedStructure),
            new LogEventProperty("Items", new SequenceValue([
                new ScalarValue(1),
                new ScalarValue(2)
            ]))
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Complex"] = structure
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        var complex = props["Complex"].AsBsonDocument;

        complex["Name"].AsString.Should().Be("Test");
        complex["Nested"].AsBsonDocument["InnerProp"].AsString.Should().Be("inner");
        complex["Items"].AsBsonArray.Count.Should().Be(2);
    }

    [Fact]
    public void CreateDocument_WithMessageTemplate_ShouldRenderMessage()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Name"] = new ScalarValue("John"),
            ["Age"] = new ScalarValue(30)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "User {Name} is {Age} years old",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document["Message"].AsString.Should().Be("User \"John\" is 30 years old");
    }

    [Fact]
    public void CreateDocument_WithDateTimeProperty_ShouldConvertToBsonDateTime()
    {
        // Arrange
        var dateTime = DateTime.UtcNow;
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Timestamp"] = new ScalarValue(dateTime)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Timestamp"].Should().BeOfType<BsonDateTime>();

        var actualTime = props["Timestamp"].AsBsonDateTime.ToUniversalTime();

        (actualTime - dateTime).TotalMilliseconds.Should().BeLessThan(1);
    }

    [Fact]
    public void CreateDocument_WithGuidProperty_ShouldConvertToBsonBinaryData()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["CorrelationId"] = new ScalarValue(guid)
        };
        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props.Contains("CorrelationId").Should().BeTrue();
        props["CorrelationId"].Should().BeOfType<BsonBinaryData>();
        var binaryData = props["CorrelationId"].AsBsonBinaryData;
        binaryData.SubType.Should().Be(BsonBinarySubType.UuidStandard);
        binaryData.AsGuid.Should().Be(guid);
    }

    [Fact]
    public void CreateDocument_WithLongProperty_ShouldConvertToBsonInt64()
    {
        // Arrange
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["LongValue"] = new ScalarValue(9223372036854775807L)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["LongValue"].Should().BeOfType<BsonInt64>();
        props["LongValue"].AsInt64.Should().Be(9223372036854775807L);
    }

    [Fact]
    public void CreateDocument_WithDecimalProperty_ShouldConvertToBsonDecimal128()
    {
        // Arrange
        var decimalValue = 123.456m;
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Amount"] = new ScalarValue(decimalValue)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Amount"].Should().BeOfType<BsonDecimal128>();
        props["Amount"].AsDecimal.Should().Be(decimalValue);
    }

    [Fact]
    public void CreateDocument_WithDateTimeOffsetProperty_ShouldConvertToBsonDateTime()
    {
        // Arrange
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Timestamp"] = new ScalarValue(dateTimeOffset)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Timestamp"].Should().BeOfType<BsonDateTime>();
        var actualTime = props["Timestamp"].AsBsonDateTime.ToUniversalTime();
        (actualTime - dateTimeOffset.UtcDateTime).TotalMilliseconds.Should().BeLessThan(1);
    }

    [Fact]
    public void CreateDocument_WithTimeSpanProperty_ShouldConvertToString()
    {
        // Arrange
        var timeSpan = TimeSpan.FromHours(2.5);
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Duration"] = new ScalarValue(timeSpan)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Duration"].Should().BeOfType<BsonString>();
        props["Duration"].AsString.Should().Be(timeSpan.ToString());
    }

    [Fact]
    public void CreateDocument_WithDateOnlyProperty_ShouldConvertToBsonDateTime()
    {
        // Arrange
        var dateOnly = new DateOnly(2024, 1, 15);
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Date"] = new ScalarValue(dateOnly)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Date"].Should().BeOfType<BsonDateTime>();
        var actualDate = props["Date"].AsBsonDateTime.ToUniversalTime();
        actualDate.Date.Should().Be(dateOnly.ToDateTime(TimeOnly.MinValue));
    }

    [Fact]
    public void CreateDocument_WithTimeOnlyProperty_ShouldConvertToString()
    {
        // Arrange
        var timeOnly = new TimeOnly(14, 30, 45);
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Time"] = new ScalarValue(timeOnly)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["Time"].Should().BeOfType<BsonString>();
        props["Time"].AsString.Should().Be(timeOnly.ToString("O"));
    }

    [Fact]
    public void CreateDocument_WithEmptySequenceProperty_ShouldCreateEmptyBsonArray()
    {
        // Arrange
        var sequence = new SequenceValue([]);
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["EmptyArray"] = sequence
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        props["EmptyArray"].Should().BeOfType<BsonArray>();
        props["EmptyArray"].AsBsonArray.Count.Should().Be(0);
    }

    [Fact]
    public void CreateDocument_WithMixedTypeSequence_ShouldPreserveTypes()
    {
        // Arrange
        var sequence = new SequenceValue([
            new ScalarValue(1),
            new ScalarValue("text"),
            new ScalarValue(true),
            new ScalarValue(3.14)
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["MixedArray"] = sequence
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        var props = document["Properties"].AsBsonDocument;
        var array = props["MixedArray"].AsBsonArray;

        array.Count.Should().Be(4);
        array[0].AsInt32.Should().Be(1);
        array[1].AsString.Should().Be("text");
        array[2].AsBoolean.Should().BeTrue();
        array[3].AsDouble.Should().Be(3.14);
    }

    [Fact]
    public void CreateDocument_WithLogEventBelowMinimumLevel_ShouldReturnNull()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = LogEventLevel.Warning
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "This should be filtered out"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().BeNull();
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, LogEventLevel.Debug, null)]
    [InlineData(LogEventLevel.Debug, LogEventLevel.Information, null)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Warning, null)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Error, null)]
    [InlineData(LogEventLevel.Error, LogEventLevel.Fatal, null)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, LogEventLevel.Warning, "Error")]
    [InlineData(LogEventLevel.Fatal, LogEventLevel.Warning, "Fatal")]
    public void CreateDocument_WithMinimumLevel_FiltersCorrectly(
        LogEventLevel logLevel,
        LogEventLevel minimumLevel,
        string? expectedLevel)
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = minimumLevel
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            logLevel,
            "Test message"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        if (expectedLevel == null)
        {
            document.Should().BeNull();
        }
        else
        {
            document.Should().NotBeNull();
            document!["Level"].AsString.Should().Be(expectedLevel);
        }
    }

    [Fact]
    public void CreateDocument_WithLogEventAtMinimumLevel_ShouldCreateDocument()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = LogEventLevel.Information
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "This should be included"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!["Level"].AsString.Should().Be("Information");
        document["Message"].AsString.Should().Be("This should be included");
    }

    [Fact]
    public void CreateDocument_WithLogEventAboveMinimumLevel_ShouldCreateDocument()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = LogEventLevel.Information
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "This should be included"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!["Level"].AsString.Should().Be("Error");
        document["Message"].AsString.Should().Be("This should be included");
    }

    [Fact]
    public void CreateDocument_WithMinimumLevelVerbose_ShouldAllowAllLevels()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = LogEventLevel.Verbose
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Verbose,
            "Verbose message"
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!["Level"].AsString.Should().Be("Verbose");
    }

    [Fact]
    public void CreateDocument_WithMinimumLevelFatal_ShouldOnlyAllowFatal()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            MinimumLevel = LogEventLevel.Fatal
        };

        // Act - Error should be filtered
        var errorEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            "Error message"
        );
        var errorDocument = _factory.CreateDocument(errorEvent, options);

        // Act - Fatal should be allowed
        var fatalEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Fatal,
            "Fatal message"
        );
        var fatalDocument = _factory.CreateDocument(fatalEvent, options);

        // Assert
        errorDocument.Should().BeNull();
        fatalDocument.Should().NotBeNull();
        fatalDocument!["Level"].AsString.Should().Be("Fatal");
    }

    [Fact]
    public void CreateDocument_WithPromotedProperty_ShouldAddToTopLevel()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["UserId"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345"),
            ["RequestId"] = new ScalarValue("abc-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document["UserId"].AsString.Should().Be("12345");
        
        // Non-promoted property should still be in Properties
        document.Contains("Properties").Should().BeTrue();
        var props = document["Properties"].AsBsonDocument;
        props.Contains("RequestId").Should().BeTrue();
        props["RequestId"].AsString.Should().Be("abc-123");
        
        // Promoted property should not be in Properties
        props.Contains("UserId").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithMultiplePromotedProperties_ShouldAddAllToTopLevel()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["UserId", "CorrelationId", "TenantId"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345"),
            ["CorrelationId"] = new ScalarValue(Guid.NewGuid()),
            ["TenantId"] = new ScalarValue("tenant-1"),
            ["RequestId"] = new ScalarValue("request-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document["UserId"].AsString.Should().Be("12345");
        document.Contains("CorrelationId").Should().BeTrue();
        document.Contains("TenantId").Should().BeTrue();
        document["TenantId"].AsString.Should().Be("tenant-1");

        // Non-promoted property should be in Properties
        var props = document["Properties"].AsBsonDocument;
        props.Contains("RequestId").Should().BeTrue();
        
        // Promoted properties should not be in Properties
        props.Contains("UserId").Should().BeFalse();
        props.Contains("CorrelationId").Should().BeFalse();
        props.Contains("TenantId").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithPromotedPropertyCaseInsensitive_ShouldMatch()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["userid"]  // lowercase
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345")  // mixed case
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document["UserId"].AsString.Should().Be("12345");
        document.Contains("Properties").Should().BeFalse(); // No other properties
    }

    [Fact]
    public void CreateDocument_WithPromotedPropertyNotInLogEvent_ShouldNotAddToDocument()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["UserId", "NonExistent"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document.Contains("NonExistent").Should().BeFalse();
        document.Contains("Properties").Should().BeFalse(); // No remaining properties
    }

    [Fact]
    public void CreateDocument_WithEmptyPropertiesCollection_ShouldNotPromoteAnything()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = []
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345"),
            ["RequestId"] = new ScalarValue("abc-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeFalse(); // Not at top level
        document.Contains("RequestId").Should().BeFalse(); // Not at top level
        
        // All properties should be in Properties subdocument
        var props = document["Properties"].AsBsonDocument;
        props.Contains("UserId").Should().BeTrue();
        props.Contains("RequestId").Should().BeTrue();
    }

    [Fact]
    public void CreateDocument_WithNullPropertiesCollection_ShouldNotPromoteAnything()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = null
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345"),
            ["RequestId"] = new ScalarValue("abc-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeFalse(); // Not at top level
        document.Contains("RequestId").Should().BeFalse(); // Not at top level
        
        // All properties should be in Properties subdocument
        var props = document["Properties"].AsBsonDocument;
        props.Contains("UserId").Should().BeTrue();
        props.Contains("RequestId").Should().BeTrue();
    }

    [Fact]
    public void CreateDocument_WithPromotedComplexProperty_ShouldPreserveType()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["User"]
        };

        var structure = new StructureValue([
            new LogEventProperty("Name", new ScalarValue("John")),
            new LogEventProperty("Age", new ScalarValue(30))
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["User"] = structure
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("User").Should().BeTrue();
        document["User"].Should().BeOfType<BsonDocument>();
        
        var user = document["User"].AsBsonDocument;
        user["Name"].AsString.Should().Be("John");
        user["Age"].AsInt32.Should().Be(30);
        
        // Should not be in Properties subdocument
        document.Contains("Properties").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithPromotedArrayProperty_ShouldPreserveType()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["Tags"]
        };

        var sequence = new SequenceValue([
            new ScalarValue("tag1"),
            new ScalarValue("tag2"),
            new ScalarValue("tag3")
        ]);

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Tags"] = sequence
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("Tags").Should().BeTrue();
        document["Tags"].Should().BeOfType<BsonArray>();
        
        var tags = document["Tags"].AsBsonArray;
        tags.Count.Should().Be(3);
        tags[0].AsString.Should().Be("tag1");
        tags[1].AsString.Should().Be("tag2");
        tags[2].AsString.Should().Be("tag3");
    }

    [Fact]
    public void CreateDocument_WithPromotedPropertyWithInvalidCharacters_ShouldSanitize()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["User.Id"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["User.Id"] = new ScalarValue("12345")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("User.Id").Should().BeFalse();
        document.Contains("User_Id").Should().BeTrue();
        document["User_Id"].AsString.Should().Be("12345");
    }

    [Fact]
    public void CreateDocument_WithAllPropertiesPromoted_ShouldNotIncludePropertiesField()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["UserId", "RequestId"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue("12345"),
            ["RequestId"] = new ScalarValue("abc-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document.Contains("RequestId").Should().BeTrue();
        
        // No Properties field should exist since all were promoted
        document.Contains("Properties").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithDefaultSourceContextPromotion_ShouldPromoteSourceContext()
    {
        // Arrange
        // Default options has SourceContext in promoted properties
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["SourceContext"] = new ScalarValue("MyApp.Services.UserService"),
            ["RequestId"] = new ScalarValue("abc-123")
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, _options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("SourceContext").Should().BeTrue();
        document["SourceContext"].AsString.Should().Be("MyApp.Services.UserService");
        
        // RequestId should be in Properties
        var props = document["Properties"].AsBsonDocument;
        props.Contains("RequestId").Should().BeTrue();
        props.Contains("SourceContext").Should().BeFalse();
    }

    [Fact]
    public void CreateDocument_WithPromotedNullValue_ShouldAddBsonNull()
    {
        // Arrange
        var options = new MongoSinkOptions
        {
            Properties = ["UserId"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["UserId"] = new ScalarValue(null)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("UserId").Should().BeTrue();
        document["UserId"].Should().BeOfType<BsonNull>();
    }

    [Fact]
    public void CreateDocument_WithPromotedGuidProperty_ShouldConvertToBsonBinaryData()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var options = new MongoSinkOptions
        {
            Properties = ["CorrelationId"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["CorrelationId"] = new ScalarValue(guid)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("CorrelationId").Should().BeTrue();
        document["CorrelationId"].Should().BeOfType<BsonBinaryData>();
        document["CorrelationId"].AsBsonBinaryData.AsGuid.Should().Be(guid);
    }

    [Fact]
    public void CreateDocument_WithPromotedDateTimeProperty_ShouldConvertToBsonDateTime()
    {
        // Arrange
        var dateTime = DateTime.UtcNow;
        var options = new MongoSinkOptions
        {
            Properties = ["EventTime"]
        };

        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["EventTime"] = new ScalarValue(dateTime)
        };

        var logEvent = CreateLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            "Test",
            properties: properties
        );

        // Act
        var document = _factory.CreateDocument(logEvent, options);

        // Assert
        document.Should().NotBeNull();
        document!.Contains("EventTime").Should().BeTrue();
        document["EventTime"].Should().BeOfType<BsonDateTime>();
        
        var actualTime = document["EventTime"].AsBsonDateTime.ToUniversalTime();
        (actualTime - dateTime).TotalMilliseconds.Should().BeLessThan(1);
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
}
