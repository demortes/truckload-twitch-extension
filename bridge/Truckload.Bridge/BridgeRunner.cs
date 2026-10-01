using Truckload.Bridge.Mapping;
using Truckload.Bridge.Sending;
using Truckload.Contracts;

namespace Truckload.Bridge;

public enum BridgeExitReason { Cancelled, InvalidKey }

/// <summary>
/// The main polling loop: fetch from the telemetry source, map to the canonical contract,
/// and send only when something changed or a heartbeat is due — never faster than once a second.
/// </summary>
public sealed class BridgeRunner
{
    private readonly ITelemetrySource _source;
    private readonly IngestSender? _sender;
    private readonly BridgeOptions _options;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;

    /// <summary>
    /// Consecutive failed polls tolerated before the bridge tells viewers the game is disconnected.
    /// A single failed poll (a dropped local connection, TruckTel busy loading a save) must not flip
    /// the panel to OFFLINE and back.
    /// </summary>
    public const int DisconnectGraceTicks = 3;

    private static readonly TimeSpan MinSendInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    public BridgeRunner(ITelemetrySource source, IngestSender? sender, BridgeOptions options, TimeProvider time, Action<string>? log = null)
    {
        _source = source;
        _sender = sender;
        _options = options;
        _time = time;
        _log = log ?? Console.WriteLine;
    }

    /// <summary>Runs one iteration of fetch → decide → (optionally) send, returning the decision made. Exposed for tests.</summary>
    public async Task<TelemetryPayload?> TickAsync(RunnerState state, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var nowUnix = now.ToUnixTimeSeconds();

        var fetch = await _source.FetchAsync(cancellationToken);

        if (fetch.Available)
        {
            state.ConsecutiveUnavailable = 0;
        }
        else if (++state.ConsecutiveUnavailable < DisconnectGraceTicks)
        {
            if (_options.Verbose)
                _log($"[info] telemetry source unavailable ({state.ConsecutiveUnavailable}/{DisconnectGraceTicks}), waiting before reporting disconnected");
            return null;
        }

        var payload = fetch.Available
            ? TelemetryMapper.Map(fetch.Data!, nowUnix, _options.Units)
            : TelemetryMapper.Disconnected(nowUnix);

        if (!fetch.Available && state.LastLoggedUnavailable != true && fetch.Error is not null)
            _log($"[warn] {fetch.Error}");
        state.LastLoggedUnavailable = !fetch.Available;

        // Event detection compares this tick's truck info to the previous tick's, regardless
        // of whether that previous tick was actually sent (unlike LastSent below, which only
        // tracks what was last decided worth sending) — so a crash right after a heartbeat
        // resend is still caught on the very next poll.
        var events = TelemetryEventDetector.Detect(state.LastTruck, payload.Truck);
        if (events is not null)
        {
            payload = payload with { Events = events };
            if (_options.Verbose)
                _log($"[info] event detected: {string.Join(", ", events.Select(e => e.Type))}");
        }
        state.LastTruck = payload.Truck;

        var changed = state.LastSent is null || !PayloadsEqualIgnoringTimestamp(state.LastSent, payload);
        var heartbeatDue = state.LastSentAt is null || now - state.LastSentAt >= TimeSpan.FromSeconds(_options.HeartbeatSeconds);
        var minIntervalOk = state.LastSentAt is null || now - state.LastSentAt >= MinSendInterval;
        var backoffOk = state.BackoffUntil is null || now >= state.BackoffUntil;

        if (!((changed || heartbeatDue) && minIntervalOk && backoffOk))
            return null;

        if (_options.Verbose)
            _log($"[info] sending: connected={payload.Connected} job={(payload.Job is null ? "none" : payload.Job.Destination)}");

        if (_options.DryRun || _sender is null)
        {
            state.LastSent = payload;
            state.LastSentAt = now;
            return payload;
        }

        var result = await _sender.SendAsync(payload, cancellationToken);
        switch (result.Outcome)
        {
            case SendOutcome.Sent:
            case SendOutcome.Throttled:
                state.LastSent = payload;
                state.LastSentAt = now;
                state.BackoffUntil = null;
                state.CurrentBackoff = null;
                break;

            case SendOutcome.RateLimited:
                state.LastSent = payload;
                state.LastSentAt = now;
                state.BackoffUntil = now.Add(result.RetryAfter ?? TimeSpan.FromSeconds(5));
                break;

            case SendOutcome.InvalidKey:
                state.ExitRequested = BridgeExitReason.InvalidKey;
                _log("[error] The backend rejected the ingest key. Regenerate it on the extension's Config page.");
                break;

            case SendOutcome.Failed:
            case SendOutcome.NetworkError:
                state.CurrentBackoff = state.CurrentBackoff is { } current
                    ? TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2, MaxBackoff.TotalSeconds))
                    : InitialBackoff;
                state.BackoffUntil = now.Add(state.CurrentBackoff.Value);
                _log($"[warn] Failed to send telemetry ({result.Detail}); retrying in {state.CurrentBackoff.Value.TotalSeconds:0}s.");
                break;
        }

        return payload;
    }

    public async Task<RunnerState> RunAsync(CancellationToken cancellationToken)
    {
        var state = new RunnerState();

        while (!cancellationToken.IsCancellationRequested && state.ExitRequested is null)
        {
            await TickAsync(state, cancellationToken);

            if (state.ExitRequested is not null)
                break;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollMs), _time, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return state;
    }

    private static bool PayloadsEqualIgnoringTimestamp(TelemetryPayload a, TelemetryPayload b) =>
        (a with { Ts = 0 }) == (b with { Ts = 0 });
}

public sealed class RunnerState
{
    public TelemetryPayload? LastSent { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }
    public DateTimeOffset? BackoffUntil { get; set; }
    public TimeSpan? CurrentBackoff { get; set; }
    public bool? LastLoggedUnavailable { get; set; }

    /// <summary>How many polls in a row have failed; see <see cref="BridgeRunner.DisconnectGraceTicks"/>.</summary>
    public int ConsecutiveUnavailable { get; set; }
    public BridgeExitReason? ExitRequested { get; set; }

    /// <summary>The truck info from the previous tick, used by <see cref="TelemetryEventDetector"/>.
    /// Tracked independently of <see cref="LastSent"/> so event detection compares every poll,
    /// not just polls whose payload was actually sent.</summary>
    public TruckInfo? LastTruck { get; set; }
}
