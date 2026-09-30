using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Truckload.Ebs.Logging;

/// <summary>
/// Single-line JSON console formatter using Datadog's reserved log attributes, so no
/// pipeline remapping is needed: <c>timestamp</c>, <c>level</c> (status), <c>message</c>,
/// <c>logger.name</c>, <c>error.*</c>, and top-level scope/state properties
/// (including the tracer's injected <c>dd.trace_id</c> / <c>dd.span_id</c>).
/// </summary>
public sealed class DatadogJsonFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "datadog-json";

    public override void Write<TState>(
        in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
            return;

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            w.WriteString("level", MapLevel(logEntry.LogLevel));
            w.WriteString("message", message ?? logEntry.Exception!.Message);
            w.WriteString("logger.name", logEntry.Category);
            if (logEntry.EventId.Id != 0)
                w.WriteNumber("event.id", logEntry.EventId.Id);

            var written = new HashSet<string> { "timestamp", "level", "message", "logger.name", "event.id", "error" };

            if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> state)
                foreach (var kv in state)
                    WriteProperty(w, written, kv.Key, kv.Value);

            scopeProvider?.ForEachScope((scope, writer) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    foreach (var kv in pairs)
                        WriteProperty(writer.w, writer.written, kv.Key, kv.Value);
            }, (w, written));

            if (logEntry.Exception is { } ex)
            {
                w.WriteStartObject("error");
                w.WriteString("kind", ex.GetType().FullName);
                w.WriteString("message", ex.Message);
                w.WriteString("stack", ex.ToString());
                w.WriteEndObject();
            }

            w.WriteEndObject();
        }

        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length));
    }

    private static void WriteProperty(Utf8JsonWriter w, HashSet<string> written, string key, object? value)
    {
        // "{OriginalFormat}" is the message template and "newLine" is EF's line-separator
        // state; neither is a useful searchable attribute.
        if (key is "{OriginalFormat}" or "newLine" || !written.Add(key))
            return;

        switch (value)
        {
            case null: w.WriteNull(key); break;
            case bool b: w.WriteBoolean(key, b); break;
            case int i: w.WriteNumber(key, i); break;
            case long l: w.WriteNumber(key, l); break;
            case double d when double.IsFinite(d): w.WriteNumber(key, d); break;
            default: w.WriteString(key, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)); break;
        }
    }

    // Datadog status values: debug, info, warn/warning, error, critical.
    private static string MapLevel(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => "debug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "error",
        LogLevel.Critical => "critical",
        _ => "info",
    };
}
