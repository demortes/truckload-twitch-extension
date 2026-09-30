using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Truckload.Ebs.Logging;
using Xunit;

namespace Truckload.Ebs.Tests;

public class DatadogJsonFormatterTests
{
    private sealed class Scopes : IExternalScopeProvider
    {
        public List<object> Items { get; } = [];
        public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
        {
            foreach (var i in Items) callback(i, state);
        }
        public IDisposable Push(object? state) => throw new NotSupportedException();
    }

    // Really throws and catches so the exception carries a stack trace, as it would in production.
    private static Exception CaptureThrown()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException caught)
        {
            return caught;
        }
    }

    private static JsonElement Format(LogLevel level, string message, KeyValuePair<string, object?>[] state, Exception? ex = null, Scopes? scopes = null)
    {
        var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
            level, "Cat", new EventId(0), state, ex, (_, _) => message);
        var writer = new StringWriter();
        new DatadogJsonFormatter().Write(entry, scopes, writer);
        var line = writer.ToString().TrimEnd();
        Assert.DoesNotContain('\n', line);
        return JsonDocument.Parse(line).RootElement;
    }

    [Theory]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Information, "info")]
    [InlineData(LogLevel.Warning, "warn")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Critical, "critical")]
    public void Maps_level_to_datadog_status(LogLevel level, string expected)
    {
        var json = Format(level, "hello", []);
        Assert.Equal(expected, json.GetProperty("level").GetString());
        Assert.Equal("hello", json.GetProperty("message").GetString());
        Assert.Equal("Cat", json.GetProperty("logger.name").GetString());
    }

    [Fact]
    public void Flattens_state_and_dd_scope_and_writes_error_block()
    {
        var scopes = new Scopes();
        scopes.Items.Add(new Dictionary<string, object?> { ["dd.trace_id"] = "123", ["dd.span_id"] = "456" });

        var json = Format(LogLevel.Error, "failed for c1", [new("ChannelId", "c1"), new("{OriginalFormat}", "failed for {ChannelId}"), new("newLine", "\n")], CaptureThrown(), scopes);

        Assert.Equal("c1", json.GetProperty("ChannelId").GetString());
        Assert.Equal("123", json.GetProperty("dd.trace_id").GetString());
        Assert.Equal("456", json.GetProperty("dd.span_id").GetString());
        var error = json.GetProperty("error");
        Assert.Equal("System.InvalidOperationException", error.GetProperty("kind").GetString());
        Assert.Equal("boom", error.GetProperty("message").GetString());
        Assert.False(json.TryGetProperty("{OriginalFormat}", out _));
        Assert.False(json.TryGetProperty("newLine", out _));
    }
}
