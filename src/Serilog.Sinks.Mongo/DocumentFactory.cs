using System.Runtime.InteropServices;

using MongoDB.Bson;

using Serilog.Events;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Default implementation of <see cref="IDocumentFactory"/> that converts Serilog log events to BSON documents for MongoDB storage.
/// </summary>
public class DocumentFactory : IDocumentFactory
{
    /// <summary>
    /// Creates a BSON document from a Serilog log event.
    /// </summary>
    /// <param name="logEvent">The Serilog log event to convert.</param>
    /// <param name="options">The MongoDB sink options that control document creation.</param>
    /// <returns>
    /// A BSON document containing the log event data, or <c>null</c> if the log event should not be persisted
    /// (e.g., when the log level is below the minimum level or the log event is null).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The created document includes:
    /// <list type="bullet">
    /// <item><description>Timestamp - The UTC timestamp of the log event</description></item>
    /// <item><description>Level - The log level (Verbose, Debug, Information, Warning, Error, Fatal)</description></item>
    /// <item><description>Message - The rendered log message</description></item>
    /// <item><description>TraceId - The distributed trace ID (if available)</description></item>
    /// <item><description>SpanId - The span ID for distributed tracing (if available)</description></item>
    /// <item><description>Exception - Detailed exception information (if an exception is present)</description></item>
    /// <item><description>Properties - Additional log event properties</description></item>
    /// <item><description>Promoted properties - Properties configured to appear at the document root level</description></item>
    /// </list>
    /// </remarks>
    public virtual BsonDocument? CreateDocument(LogEvent logEvent, MongoSinkOptions options)
    {
        if (logEvent == null)
            return null;

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // Don't create document if log level is below minimum
        if (logEvent.Level < options.MinimumLevel)
            return null;

        var document = new BsonDocument
        {
            { MongoSinkDefaults.Timestamp, new BsonDateTime(logEvent.Timestamp.UtcDateTime) },
            { MongoSinkDefaults.Level, new BsonString(ConvertLevel(logEvent.Level)) },
            { MongoSinkDefaults.Message, new BsonString(logEvent.RenderMessage()) }
        };

        if (logEvent.TraceId != null)
            document[MongoSinkDefaults.TraceId] = new BsonString(logEvent.TraceId.Value.ToHexString());

        if (logEvent.SpanId != null)
            document[MongoSinkDefaults.SpanId] = new BsonString(logEvent.SpanId.Value.ToHexString());

        // promote configured properties to top-level
        PromoteProperties(logEvent, options, document);

        var exceptionDocument = CreateException(logEvent.Exception);
        if (exceptionDocument != null)
            document[MongoSinkDefaults.Exception] = exceptionDocument;

        // remove promoted properties from the Properties document if enabled
        var ignored = options.OptimizeProperties ? options.Properties : null;

        var propertyDocument = CreateProperties(logEvent.Properties, ignored);
        if (propertyDocument != null)
            document[MongoSinkDefaults.Properties] = propertyDocument;

        return document;
    }

    /// <summary>
    /// Promotes configured properties from the log event to the top level of the BSON document.
    /// </summary>
    /// <param name="logEvent">The log event containing properties to promote.</param>
    /// <param name="options">The sink options specifying which properties to promote.</param>
    /// <param name="document">The BSON document to add promoted properties to.</param>
    /// <remarks>
    /// Properties are promoted based on the <see cref="MongoSinkOptions.Properties"/> collection.
    /// Property names are sanitized to comply with MongoDB field name restrictions.
    /// </remarks>
    private static void PromoteProperties(LogEvent logEvent, MongoSinkOptions options, BsonDocument document)
    {
        if (options.Properties == null)
            return;

        foreach (var logProperty in logEvent.Properties)
        {
            if (!options.Properties.Contains(logProperty.Key, StringComparer.OrdinalIgnoreCase))
                continue;

            var propertyName = SanitizePropertyName(logProperty.Key);
            document[propertyName] = ConvertProperty(logProperty.Value);
        }
    }

    /// <summary>
    /// Creates a BSON document from a collection of log event properties.
    /// </summary>
    /// <param name="properties">The log event properties to convert.</param>
    /// <param name="ignored">Optional set of property names to exclude from the document.</param>
    /// <returns>
    /// A BSON document containing the properties, or <c>null</c> if there are no properties to include.
    /// </returns>
    /// <remarks>
    /// Properties in the <paramref name="ignored"/> set are excluded from the result.
    /// This is typically used to exclude properties that have been promoted to the document root level.
    /// </remarks>
    private static BsonDocument? CreateProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties, HashSet<string>? ignored = null)
    {
        if (properties == null || properties.Count == 0)
            return null;

        BsonDocument? document = null;
        foreach (var property in properties)
        {
            if (ignored?.Contains(property.Key, StringComparer.OrdinalIgnoreCase) == true)
                continue;

            // Lazily create the document only if there are properties to add
            document ??= new BsonDocument();

            var propertyName = SanitizePropertyName(property.Key);
            var bsonValue = ConvertProperty(property.Value);

            document.Add(propertyName, bsonValue);
        }

        return document;
    }

    /// <summary>
    /// Creates a BSON document from an exception.
    /// </summary>
    /// <param name="exception">The exception to convert.</param>
    /// <returns>
    /// A BSON document containing detailed exception information, or <c>null</c> if the exception is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// The exception document includes:
    /// <list type="bullet">
    /// <item><description>Message - The exception message</description></item>
    /// <item><description>BaseMessage - The base exception message</description></item>
    /// <item><description>Type - The exception type name</description></item>
    /// <item><description>Text - The full exception string representation</description></item>
    /// <item><description>HResult - The exception HRESULT code</description></item>
    /// <item><description>ErrorCode - The error code (for ExternalException)</description></item>
    /// <item><description>Source - The exception source</description></item>
    /// <item><description>MethodName - The method where the exception occurred</description></item>
    /// <item><description>ModuleName - The assembly name</description></item>
    /// <item><description>ModuleVersion - The assembly version</description></item>
    /// </list>
    /// AggregateExceptions are flattened, and single inner exceptions are unwrapped.
    /// </remarks>
    private static BsonDocument? CreateException(Exception? exception)
    {
        if (exception == null)
            return null;

        if (exception is AggregateException aggregateException)
        {
            aggregateException = aggregateException.Flatten();
            if (aggregateException.InnerExceptions?.Count == 1)
                exception = aggregateException.InnerExceptions[0];
            else
                exception = aggregateException;
        }

        var document = new BsonDocument
        {
            { MongoSinkDefaults.Message, new BsonString(exception.Message) },
            { MongoSinkDefaults.BaseMessage, new BsonString(exception.GetBaseException().Message) },
            { MongoSinkDefaults.Type, new BsonString(exception.GetType().ToString()) },
            { MongoSinkDefaults.Text, new BsonString(exception.ToString()) },
        };

        if (exception is ExternalException external)
            document.Add(MongoSinkDefaults.ErrorCode, new BsonInt32(external.ErrorCode));

        document.Add(MongoSinkDefaults.HResult, new BsonInt32(exception.HResult));

        if (!string.IsNullOrEmpty(exception.Source))
            document.Add(MongoSinkDefaults.Source, new BsonString(exception.Source));

        var method = exception.TargetSite;
        if (method == null)
            return document;

        document.Add(MongoSinkDefaults.MethodName, new BsonString(method.Name));

        var assembly = method.Module?.Assembly?.GetName();
        if (assembly != null)
        {
            if (!string.IsNullOrEmpty(assembly.Name))
                document.Add(MongoSinkDefaults.ModuleName, new BsonString(assembly.Name));

            if (assembly.Version != null)
                document.Add(MongoSinkDefaults.ModuleVersion, new BsonString(assembly.Version.ToString()));
        }

        return document;
    }

    /// <summary>
    /// Converts a Serilog property value to a BSON value.
    /// </summary>
    /// <param name="propertyValue">The Serilog property value to convert.</param>
    /// <returns>A BSON value representation of the property value.</returns>
    /// <remarks>
    /// Handles the following Serilog property types:
    /// <list type="bullet">
    /// <item><description>ScalarValue - Converted to appropriate BSON types</description></item>
    /// <item><description>SequenceValue - Converted to BsonArray</description></item>
    /// <item><description>StructureValue - Converted to nested BsonDocument</description></item>
    /// <item><description>DictionaryValue - Converted to BsonDocument</description></item>
    /// </list>
    /// </remarks>
    private static BsonValue? ConvertProperty(LogEventPropertyValue propertyValue)
    {
        return propertyValue switch
        {
            ScalarValue scalarValue => ConvertScalarValue(scalarValue),
            SequenceValue sequenceValue => new BsonArray(sequenceValue.Elements.Select(ConvertProperty)),
            StructureValue structureValue => CreateProperties(structureValue.Properties.ToDictionary(static p => p.Name, static p => p.Value)),
            DictionaryValue dictionaryValue => CreateProperties(dictionaryValue.Elements.ToDictionary(static e => e.Key.ToString(), static e => e.Value)),
            _ => BsonValue.Create(propertyValue.ToString())
        };
    }

    /// <summary>
    /// Converts a Serilog scalar value to a BSON value.
    /// </summary>
    /// <param name="scalarValue">The scalar value to convert.</param>
    /// <returns>A BSON value representation of the scalar value.</returns>
    /// <remarks>
    /// Provides special handling for:
    /// <list type="bullet">
    /// <item><description>Guid - Stored as BsonBinaryData</description></item>
    /// <item><description>DateTimeOffset - Converted to UTC DateTime</description></item>
    /// <item><description>TimeSpan - Stored as string</description></item>
    /// <item><description>DateOnly - Converted to BsonDateTime (.NET 6+)</description></item>
    /// <item><description>TimeOnly - Stored as ISO 8601 string (.NET 6+)</description></item>
    /// </list>
    /// For unsupported types, falls back to string representation.
    /// </remarks>
    private static BsonValue ConvertScalarValue(ScalarValue scalarValue)
    {
        if (scalarValue.Value == null)
            return BsonNull.Value;

        // Handle types that BsonValue.Create doesn't support or need special handling
        return scalarValue.Value switch
        {
            Guid guid => new BsonBinaryData(guid, GuidRepresentation.Standard),
            DateTimeOffset dateTimeOffset => new BsonDateTime(dateTimeOffset.UtcDateTime),
            TimeSpan timeSpan => new BsonString(timeSpan.ToString()),
#if NET6_0_OR_GREATER
            DateOnly dateOnly => new BsonDateTime(dateOnly.ToDateTime(TimeOnly.MinValue)),
            TimeOnly timeOnly => new BsonString(timeOnly.ToString("O")),
#endif
            _ => CreateBsonValueSafe(scalarValue.Value)
        };
    }

    /// <summary>
    /// Safely creates a BSON value from an object, with fallback to string representation.
    /// </summary>
    /// <param name="value">The object to convert to BSON.</param>
    /// <returns>A BSON value, or a string representation if the type is not directly supported.</returns>
    /// <remarks>
    /// Attempts to use <see cref="BsonValue.Create"/> first. If that fails with an <see cref="ArgumentException"/>,
    /// falls back to storing the value's string representation.
    /// </remarks>
    private static BsonValue CreateBsonValueSafe(object value)
    {
        try
        {
            return BsonValue.Create(value);
        }
        catch (ArgumentException)
        {
            // Fallback to string representation for unsupported types
            return new BsonString(value.ToString() ?? string.Empty);
        }
    }

    /// <summary>
    /// Converts a Serilog log event level to its string representation.
    /// </summary>
    /// <param name="level">The log event level to convert.</param>
    /// <returns>The string name of the log level.</returns>
    /// <remarks>
    /// Provides optimized conversion for all standard Serilog log levels.
    /// Unknown levels default to "Verbose".
    /// </remarks>
    private static string ConvertLevel(LogEventLevel level)
    {
        // fast path for common levels
        return level switch
        {
            LogEventLevel.Verbose => nameof(LogEventLevel.Verbose),
            LogEventLevel.Debug => nameof(LogEventLevel.Debug),
            LogEventLevel.Information => nameof(LogEventLevel.Information),
            LogEventLevel.Warning => nameof(LogEventLevel.Warning),
            LogEventLevel.Error => nameof(LogEventLevel.Error),
            LogEventLevel.Fatal => nameof(LogEventLevel.Fatal),
            _ => nameof(LogEventLevel.Verbose)
        };
    }

    /// <summary>
    /// Sanitizes a property name to comply with MongoDB field name restrictions.
    /// </summary>
    /// <param name="name">The property name to sanitize.</param>
    /// <returns>A sanitized property name safe for use as a MongoDB field name.</returns>
    /// <remarks>
    /// MongoDB field names cannot contain:
    /// <list type="bullet">
    /// <item><description>Dot (.) - Reserved for nested field access</description></item>
    /// <item><description>Dollar sign ($) - Reserved for operators</description></item>
    /// <item><description>Control characters - Including null characters</description></item>
    /// </list>
    /// Invalid characters are replaced with underscores. Empty or null names are replaced with "_".
    /// </remarks>
    private static string SanitizePropertyName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";

        // Fast path: check if cleaning is needed
        var needsCleaning = name.Any(IsInvalid);
        if (!needsCleaning)
            return name;

        // Replace invalid characters
        return string.Create(name.Length, name, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                span[i] = IsInvalid(c) ? '_' : c;
            }
        });

        // MongoDB field name restrictions:
        // - Cannot contain '.' (used for nested field access)
        // - Cannot contain '$' (reserved for operators)
        // - Cannot contain null characters '\0'

        // JSON property name best practices:
        // - Remove control characters (0x00-0x1F)
        // - Handle whitespace and special characters
        static bool IsInvalid(char c) => c == '.' || c == '$' || char.IsControl(c);
    }
}
