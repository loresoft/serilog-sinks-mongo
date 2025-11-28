using MongoDB.Driver;

namespace Serilog.Sinks.Mongo;

public static class MongoSinkDefaults
{
    public const string ConnectionString = "mongodb://localhost:27017";

    public const string DatabaseName = "serilog";
    public const string CollectionName = "logs";

    public const string Timestamp = "Timestamp";
    public const string Level = "Level";
    public const string Message = "Message";
    public const string Exception = "Exception";
    public const string Properties = "Properties";
    public const string TraceId = "TraceId";
    public const string SpanId = "SpanId";

    public const string BaseMessage = "BaseMessage";
    public const string Type = "Type";
    public const string Text = "Text";
    public const string ErrorCode = "ErrorCode";
    public const string HResult = "HResult";
    public const string Source = "Source";
    public const string MethodName = "MethodName";
    public const string ModuleName = "ModuleName";
    public const string ModuleVersion = "ModuleVersion";

    public const string SourceContext = "SourceContext";

    public static readonly TimeSeriesOptions TimeSeriesOptions = new(Timestamp);
    public static readonly TimeSpan ExpireAfter = TimeSpan.FromDays(30);
}
