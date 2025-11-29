using MongoDB.Driver;

namespace Serilog.Sinks.Mongo;

/// <summary>
/// Provides default values and field name constants for the MongoDB sink.
/// </summary>
public static class MongoSinkDefaults
{
    /// <summary>
    /// The default MongoDB connection string.
    /// </summary>
    /// <value>"mongodb://localhost:27017"</value>
    public const string ConnectionString = "mongodb://localhost:27017";

    /// <summary>
    /// The default database name for storing log events.
    /// </summary>
    /// <value>"serilog"</value>
    public const string DatabaseName = "serilog";
    
    /// <summary>
    /// The default collection name for storing log events.
    /// </summary>
    /// <value>"logs"</value>
    public const string CollectionName = "logs";

    /// <summary>
    /// The field name for the log event timestamp.
    /// </summary>
    /// <value>"Timestamp"</value>
    public const string Timestamp = "Timestamp";
    
    /// <summary>
    /// The field name for the log event level (Verbose, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    /// <value>"Level"</value>
    public const string Level = "Level";
    
    /// <summary>
    /// The field name for the rendered log message.
    /// </summary>
    /// <value>"Message"</value>
    public const string Message = "Message";
    
    /// <summary>
    /// The field name for the exception document.
    /// </summary>
    /// <value>"Exception"</value>
    public const string Exception = "Exception";
    
    /// <summary>
    /// The field name for the properties document.
    /// </summary>
    /// <value>"Properties"</value>
    public const string Properties = "Properties";
    
    /// <summary>
    /// The field name for the distributed trace ID.
    /// </summary>
    /// <value>"TraceId"</value>
    public const string TraceId = "TraceId";
    
    /// <summary>
    /// The field name for the distributed tracing span ID.
    /// </summary>
    /// <value>"SpanId"</value>
    public const string SpanId = "SpanId";

    /// <summary>
    /// The field name for the base exception message within the exception document.
    /// </summary>
    /// <value>"BaseMessage"</value>
    public const string BaseMessage = "BaseMessage";
    
    /// <summary>
    /// The field name for the exception type within the exception document.
    /// </summary>
    /// <value>"Type"</value>
    public const string Type = "Type";
    
    /// <summary>
    /// The field name for the full exception text (ToString()) within the exception document.
    /// </summary>
    /// <value>"Text"</value>
    public const string Text = "Text";
    
    /// <summary>
    /// The field name for the error code (for ExternalException) within the exception document.
    /// </summary>
    /// <value>"ErrorCode"</value>
    public const string ErrorCode = "ErrorCode";
    
    /// <summary>
    /// The field name for the exception HRESULT within the exception document.
    /// </summary>
    /// <value>"HResult"</value>
    public const string HResult = "HResult";
    
    /// <summary>
    /// The field name for the exception source within the exception document.
    /// </summary>
    /// <value>"Source"</value>
    public const string Source = "Source";
    
    /// <summary>
    /// The field name for the method name where the exception occurred within the exception document.
    /// </summary>
    /// <value>"MethodName"</value>
    public const string MethodName = "MethodName";
    
    /// <summary>
    /// The field name for the assembly/module name within the exception document.
    /// </summary>
    /// <value>"ModuleName"</value>
    public const string ModuleName = "ModuleName";
    
    /// <summary>
    /// The field name for the assembly/module version within the exception document.
    /// </summary>
    /// <value>"ModuleVersion"</value>
    public const string ModuleVersion = "ModuleVersion";

    /// <summary>
    /// The property name for the source context, included in the default promoted properties set.
    /// </summary>
    /// <value>"SourceContext"</value>
    public const string SourceContext = "SourceContext";

    /// <summary>
    /// The default time-series options for MongoDB time-series collections.
    /// </summary>
    /// <value>A <see cref="TimeSeriesOptions"/> instance using <see cref="Timestamp"/> as the time field.</value>
    public static readonly TimeSeriesOptions TimeSeriesOptions = new(Timestamp);
    
    /// <summary>
    /// The default expiration time span for log documents.
    /// </summary>
    /// <value>30 days.</value>
    public static readonly TimeSpan ExpireAfter = TimeSpan.FromDays(30);
}
