using System.Runtime.InteropServices;

using MongoDB.Bson;

using Serilog.Events;

namespace Serilog.Sinks.Mongo;

public class DocumentFactory : IDocumentFactory
{
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
            document[MongoSinkDefaults.TraceId] = logEvent.TraceId.Value.ToHexString();

        if (logEvent.SpanId != null)
            document[MongoSinkDefaults.SpanId] = logEvent.SpanId.Value.ToHexString();

        // promote configured properties to top-level
        PromoteProperties(logEvent, options, document);

        var exceptionDocument = CreateException(logEvent.Exception);
        if (exceptionDocument != null)
            document[MongoSinkDefaults.Exception] = exceptionDocument;

        var propertyDocument = CreateProperties(logEvent.Properties, options.Properties);
        if (propertyDocument != null)
            document[MongoSinkDefaults.Properties] = propertyDocument;

        return document;
    }

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
