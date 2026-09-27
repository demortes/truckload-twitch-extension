using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Truckload.Ebs.Configuration;

namespace Truckload.Ebs.Services;

public class BroadcastThrottle : IBroadcastThrottle
{
    private readonly TimeProvider _time;
    private readonly TwitchSettings _settings;
    private readonly ConcurrentDictionary<string, long> _lastBroadcastTicks = new();
    private readonly ConcurrentDictionary<string, long> _suppressedUntilTicks = new();

    public BroadcastThrottle(IOptions<TwitchSettings> settings, TimeProvider? timeProvider = null)
    {
        _settings = settings.Value;
        _time = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(string channelId)
    {
        var now = _time.GetUtcNow();

        if (_suppressedUntilTicks.TryGetValue(channelId, out var suppressedUntil) &&
            now.UtcTicks < suppressedUntil)
        {
            return false;
        }

        var minInterval = TimeSpan.FromMilliseconds(_settings.MinBroadcastIntervalMs);

        if (_lastBroadcastTicks.TryGetValue(channelId, out var lastTicks))
        {
            var elapsed = now - new DateTimeOffset(lastTicks, TimeSpan.Zero);
            if (elapsed < minInterval)
                return false;
        }

        _lastBroadcastTicks[channelId] = now.UtcTicks;
        return true;
    }

    public void Suppress(string channelId, TimeSpan duration)
    {
        var until = _time.GetUtcNow().Add(duration);
        _suppressedUntilTicks[channelId] = until.UtcTicks;
    }
}
