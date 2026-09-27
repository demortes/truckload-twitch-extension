using System.Net;
using Microsoft.Extensions.Time.Testing;
using Truckload.Bridge.Funbit;
using Truckload.Bridge.Sending;
using Xunit;

namespace Truckload.Bridge.Tests;

public class BridgeRunnerTests
{
    private static BridgeOptions Options(int pollMs = 1000, int heartbeatS = 30, bool dryRun = true) => new()
    {
        PollMs = pollMs,
        HeartbeatSeconds = heartbeatS,
        DryRun = dryRun,
        Units = "auto",
    };

    private static FunbitTelemetry Connected(int fuel = 50) => new()
    {
        Game = new FunbitGame { Connected = true, GameName = "ATS" },
        Truck = new FunbitTruck { Make = "Peterbilt", Model = "579", Fuel = fuel, FuelCapacity = 100 },
    };

    [Fact]
    public async Task FirstTick_AlwaysSends()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var runner = new BridgeRunner(source, sender: null, Options(), time);
        var state = new RunnerState();

        var sent = await runner.TickAsync(state, CancellationToken.None);

        Assert.NotNull(sent);
        Assert.NotNull(state.LastSent);
    }

    [Fact]
    public async Task UnchangedPayload_IsNotResentBeforeHeartbeat()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var runner = new BridgeRunner(source, sender: null, Options(heartbeatS: 30), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(2));
        var secondSent = await runner.TickAsync(state, CancellationToken.None);

        Assert.Null(secondSent);
    }

    [Fact]
    public async Task UnchangedPayload_IsResentOnceHeartbeatElapses()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var runner = new BridgeRunner(source, sender: null, Options(heartbeatS: 30), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(31));
        var secondSent = await runner.TickAsync(state, CancellationToken.None);

        Assert.NotNull(secondSent);
    }

    [Fact]
    public async Task ChangedPayload_IsSentImmediately_ButNotFasterThanOneSecond()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected(fuel: 50)) };
        var runner = new BridgeRunner(source, sender: null, Options(), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);

        source.NextResult = TelemetryFetchResult.Ok(Connected(fuel: 40));
        time.Advance(TimeSpan.FromMilliseconds(200)); // less than the 1s minimum send interval
        var tooSoon = await runner.TickAsync(state, CancellationToken.None);
        Assert.Null(tooSoon);

        time.Advance(TimeSpan.FromMilliseconds(900)); // now past 1s total
        var afterMinInterval = await runner.TickAsync(state, CancellationToken.None);
        Assert.NotNull(afterMinInterval);
    }

    [Fact]
    public async Task InvalidKey_SetsExitRequested()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var http = new HttpClient(handler);
        var sender = new IngestSender(http, "https://example.test/api/ingest", "bad-key");
        var runner = new BridgeRunner(source, sender, Options(dryRun: false), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);

        Assert.Equal(BridgeExitReason.InvalidKey, state.ExitRequested);
    }

    [Fact]
    public async Task NetworkFailure_BacksOffExponentially()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var http = new HttpClient(handler);
        var sender = new IngestSender(http, "https://example.test/api/ingest", "some-key");
        var runner = new BridgeRunner(source, sender, Options(dryRun: false), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);
        Assert.Equal(2, state.CurrentBackoff!.Value.TotalSeconds);

        time.Advance(TimeSpan.FromSeconds(3)); // past the 2s backoff
        await runner.TickAsync(state, CancellationToken.None);
        Assert.Equal(4, state.CurrentBackoff!.Value.TotalSeconds);
    }

    [Fact]
    public async Task RateLimited_SuppressesUntilRetryAfter()
    {
        var time = new FakeTimeProvider();
        var source = new FakeTelemetrySource { NextResult = TelemetryFetchResult.Ok(Connected()) };
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"stored":true,"broadcast":"rate_limited","retryAfterMs":5000}""");
        var http = new HttpClient(handler);
        var sender = new IngestSender(http, "https://example.test/api/ingest", "some-key");
        var runner = new BridgeRunner(source, sender, Options(dryRun: false), time);
        var state = new RunnerState();

        await runner.TickAsync(state, CancellationToken.None);
        Assert.NotNull(state.BackoffUntil);

        time.Advance(TimeSpan.FromSeconds(2));
        source.NextResult = TelemetryFetchResult.Ok(Connected(fuel: 10)); // changed, but still backing off
        var duringBackoff = await runner.TickAsync(state, CancellationToken.None);
        Assert.Null(duringBackoff);
    }
}
